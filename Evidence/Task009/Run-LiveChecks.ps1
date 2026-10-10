[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Mode = 'Combined',
    [string]$Cli,
    [string]$Package,
    [string]$RuntimeLogDirectory,
    [string]$Scenario,
    [switch]$Smoke,
    [switch]$Blocked,
    [switch]$ReloadTest,
    [switch]$RuntimeDisabled,
    [switch]$NormalExit
)
$ErrorActionPreference = 'Stop'
# Historical helper is suspended only by this acceptance fixture, never by maintained launchers.
$legacyHelper = Join-Path $GameRoot 'BepInEx/plugins/LaunchCandidate.dll'
$originalHelper = [IO.File]::ReadAllBytes($legacyHelper)
$originalHash = (Get-FileHash -LiteralPath $legacyHelper).Hash
$backupPath = Join-Path $env:TEMP ('naval-helper-rollback-' + [Guid]::NewGuid().ToString('N') + '.dll')
[IO.File]::WriteAllBytes($backupPath, $originalHelper)
$moduleRoot = [IO.Path]::GetFullPath("$PSScriptRoot/../../../SeaPower.Mod")
$parameters = @{
    GameRoot = $GameRoot
    OutputDirectory = $OutputDirectory
    Mode = $Mode
    ApproveLoaderChanges = $true
    Smoke = $Smoke
    Blocked = $Blocked
    ReloadTest = $ReloadTest
    RuntimeDisabled = $RuntimeDisabled
    NormalExit = $NormalExit
}
if ($Cli) { $parameters.Cli = $Cli }
if ($Package) { $parameters.Package = $Package }
if ($RuntimeLogDirectory) { $parameters.RuntimeLogDirectory = $RuntimeLogDirectory }
if ($Scenario) { $parameters.Scenario = $Scenario }
$missionFixture = $null
try {
    if ($Mode -eq 'Production' -and $Smoke) {
        $missionFixture = Join-Path $GameRoot 'Sea Power_Data/StreamingAssets/user/missions/NavalCommandValidation.ini'
        if (Test-Path -LiteralPath $missionFixture) { throw 'Existing UI mission fixture must remain untouched' }
        Copy-Item "$moduleRoot/managed/harness/scenarios/MinimalCombat.ini" $missionFixture
    }
    Remove-Item -LiteralPath $legacyHelper
    & "$moduleRoot/scripts/Start-Live.ps1" @parameters
}
finally {
    if ($missionFixture -and (Test-Path -LiteralPath $missionFixture)) { Remove-Item -LiteralPath $missionFixture }
    [IO.File]::WriteAllBytes($legacyHelper, $originalHelper)
    if ((Get-FileHash -LiteralPath $legacyHelper).Hash -ne $originalHash) {
        throw "Historical helper rollback failed; backup: $backupPath"
    }
    [ordered]@{ restoredHelperSha256 = $originalHash; helperSuspendedForRun = $true; backup = $backupPath } |
        ConvertTo-Json | Set-Content $(if (Test-Path -LiteralPath $OutputDirectory) { "$OutputDirectory/helper-rollback.json" } else { "$OutputDirectory-helper-rollback.json" })
}
