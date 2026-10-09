[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CMake,
    [Parameter(Mandatory)][string]$VsDevCmd,
    [Parameter(Mandatory)][string]$VcpkgRoot,
    [Parameter(Mandatory)][string]$DependencyPrefix,
    [ValidateSet('windows-msvc-release', 'windows-msvc-debug')]
    [string]$Preset = 'windows-msvc-release',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../Evidence/Task007/reproduction'),
    [switch]$ContinueAfterTestFailure
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$testRunner = Join-Path (Split-Path $CMake) 'ctest.exe'
$environmentLines = & $env:ComSpec /d /s /c "`"$VsDevCmd`" -arch=x64 -host_arch=x64 >nul && set"
if ($LASTEXITCODE) {
    throw 'Visual Studio developer-shell initialization failed'
}
foreach ($environmentLine in $environmentLines) {
    if ($environmentLine -match '^([^=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}
$env:VCPKG_ROOT = $VcpkgRoot
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$evidenceDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$failedSuites = @()
foreach ($module in @('Gateway.Api', 'Gateway.Grpc.Api', 'Gateway.Grpc', 'SeaPower.Client', 'SeaPower.Mod')) {
    Push-Location (Join-Path $workspace $module)
    try {
        $configureArguments = @(
            '--preset'
            $Preset
            '--fresh'
            "-DFETCHCONTENT_SOURCE_DIR_NCBUILD=$workspace/Build.CMake"
            "-DVCPKG_INSTALLED_DIR=$DependencyPrefix"
            '-DVCPKG_MANIFEST_INSTALL=OFF'
        )
        & $CMake @configureArguments *> "$evidenceDirectory/$module-configure.log"
        if ($LASTEXITCODE) {
            throw "$module configure failed"
        }
        & $CMake --build "o/$Preset" --clean-first --parallel 8 *> "$evidenceDirectory/$module-build.log"
        if ($LASTEXITCODE) {
            throw "$module build failed"
        }
        & $testRunner --test-dir "o/$Preset" --output-on-failure --no-tests=error *> "$evidenceDirectory/$module-tests.log"
        if ($LASTEXITCODE) {
            $failedSuites += $module
            if (!$ContinueAfterTestFailure) {
                throw "$module tests failed"
            }
        }
    }
    finally {
        Pop-Location
    }
}
Push-Location (Join-Path $workspace 'Build.CMake')
try {
    & $CMake --preset $Preset --fresh *> "$evidenceDirectory/Build.CMake-configure.log"
    if ($LASTEXITCODE) {
        throw 'Build.CMake configure failed'
    }
    & $testRunner --test-dir "o/$Preset" --output-on-failure --no-tests=error *> "$evidenceDirectory/Build.CMake-tests.log"
    if ($LASTEXITCODE) {
        $failedSuites += 'Build.CMake'
    }
}
finally {
    Pop-Location
}
if ($failedSuites.Count) {
    throw "Failed test processes: $($failedSuites -join ', ')"
}
