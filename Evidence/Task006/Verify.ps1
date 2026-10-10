param([string]$Configuration = 'Release', [string[]]$Repositories = @('Domain.Api', 'Gateway.Api', 'Gateway.Grpc.Api', 'Gateway.Grpc', 'SeaPower.Client', 'SeaPower.Mod'))
$ErrorActionPreference = 'Stop'
$workspace = 'C:/Software/GitHub/NavalCommand'
$evidence = Join-Path $workspace 'Docs/Evidence/Task006'
$developerEnvironment = & cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat" >nul && set'
foreach ($line in $developerEnvironment) {
    if ($line -match '^([^=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process')
    }
}
$env:VCPKG_ROOT = 'C:/vcpkg'
$env:PATH = 'C:/Software/STM32CubeCLT/Ninja/bin;' + $env:PATH
$cmakeDirectory = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin'
$cmake = Join-Path $cmakeDirectory 'cmake.exe'
$ctest = Join-Path $cmakeDirectory 'ctest.exe'
$preset = 'windows-msvc-' + $Configuration.ToLowerInvariant()
$records = @()
foreach ($repository in $Repositories) {
    Push-Location (Join-Path $workspace $repository)
    try {
        $configureArguments = @('--preset', $preset, '--fresh', '-DFETCHCONTENT_SOURCE_DIR_NCBUILD=C:/Software/GitHub/NavalCommand/Build.CMake', '-DCMAKE_MAKE_PROGRAM=C:/Software/STM32CubeCLT/Ninja/bin/ninja.exe')
        if ($repository -ne 'Domain.Api') {
            $configureArguments += @('-DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix', '-DVCPKG_MANIFEST_INSTALL=OFF')
        }
        & $cmake @configureArguments *> (Join-Path $evidence "$repository-$Configuration-configure.log")
        $records += @{ Repository = $repository; Action = 'configure'; Exit = $LASTEXITCODE; Arguments = $configureArguments }
        if ($LASTEXITCODE)
        {
            throw "$repository configure failed"
        }
        foreach ($directory in @('managed', 'test/managed')) {
            if (Test-Path -LiteralPath $directory) {
                foreach ($project in Get-ChildItem -LiteralPath $directory -Filter *.csproj -Recurse) {
                    & dotnet clean $project.FullName -c $Configuration --nologo *> (Join-Path $evidence "$repository-$($project.BaseName)-$Configuration-managed-clean.log")
                    $records += @{ Repository = $repository; Action = 'managed-clean'; Project = $project.FullName; Exit = $LASTEXITCODE }
                    if ($LASTEXITCODE)
                    {
                        throw 'Managed clean failed'
                    }
                }
            }
        }
        & $cmake --build "o/$preset" --target clean *> (Join-Path $evidence "$repository-$Configuration-clean.log")
        $records += @{ Repository = $repository; Action = 'clean'; Exit = $LASTEXITCODE }
        if ($LASTEXITCODE)
        {
            throw "$repository clean failed"
        }
        & $cmake --build "o/$preset" --parallel 8 *> (Join-Path $evidence "$repository-$Configuration-build.log")
        $records += @{ Repository = $repository; Action = 'build'; Exit = $LASTEXITCODE }
        if ($LASTEXITCODE)
        {
            throw "$repository build failed"
        }
        if ($repository -eq 'SeaPower.Mod') {
            & $cmake --build "o/$preset" --parallel 8 --target package-runtime package-harness managed-tests *> (Join-Path $evidence "$repository-$Configuration-packages.log")
            $records += @{ Repository = $repository; Action = 'packages'; Exit = $LASTEXITCODE }
            if ($LASTEXITCODE)
            {
                throw 'Packaging failed'
            }
            foreach ($project in @('managed/runtime/SeaPower.Mod.csproj', 'managed/harness/SeaPower.Harness.csproj')) {
                & dotnet build $project -c $Configuration --nologo *> (Join-Path $evidence "direct-$([IO.Path]::GetFileNameWithoutExtension($project))-$Configuration.log")
                $records += @{ Repository = $repository; Action = 'direct-msbuild'; Project = $project; Exit = $LASTEXITCODE }
                if ($LASTEXITCODE)
                {
                    throw 'Direct managed build failed'
                }
            }
        }
        & $ctest --test-dir "o/$preset" --build-config $Configuration --output-on-failure --no-tests=error *> (Join-Path $evidence "$repository-$Configuration-test.log")
        $records += @{ Repository = $repository; Action = 'ctest'; Exit = $LASTEXITCODE }
        Write-Output "$repository $Configuration CTest exit $LASTEXITCODE"
    }
    finally {
            Pop-Location
        }
}
$records | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $evidence "results-$Configuration.json")
