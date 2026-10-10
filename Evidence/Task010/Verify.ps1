param([string]$Repository = 'SeaPower.Mod', [string]$Configuration = 'Release', [string]$Action = 'build', [string[]]$Targets)
$ErrorActionPreference = 'Stop'
$workspace = 'C:/Software/GitHub/NavalCommand'
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
Push-Location (Join-Path $workspace $Repository)
try {
    if ($Action -eq 'configure') {
        & $cmake --preset $preset --fresh -DFETCHCONTENT_SOURCE_DIR_NCBUILD=C:/Software/GitHub/NavalCommand/Build.CMake -DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix -DVCPKG_MANIFEST_INSTALL=OFF -DCMAKE_MAKE_PROGRAM=C:/Software/STM32CubeCLT/Ninja/bin/ninja.exe
    }
    elseif ($Action -eq 'test') {
        & $ctest --test-dir "o/$preset" --build-config $Configuration --output-on-failure --no-tests=error
    }
    else {
        $arguments = @('--build', "o/$preset", '--config', $Configuration, '--parallel', '8')
        if ($Targets) {
            $arguments += @('--target') + $Targets
        }
        & $cmake @arguments
    }
    if ($LASTEXITCODE) {
        throw "$Repository $Action failed with exit $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
