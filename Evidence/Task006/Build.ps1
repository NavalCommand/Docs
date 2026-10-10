param([string]$Repository, [string]$Target = 'all', [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$developerEnvironment = & cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat" >nul && set'
foreach ($line in $developerEnvironment) {
    if ($line -match '^([^=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process')
    }
}
$env:VCPKG_ROOT = 'C:/vcpkg'
$env:PATH = 'C:/Software/STM32CubeCLT/Ninja/bin;' + $env:PATH
& 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe' --build "$Repository/o/windows-msvc-$($Configuration.ToLowerInvariant())" --target $Target --parallel 8
exit $LASTEXITCODE
