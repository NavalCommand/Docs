param([ValidateSet('Harness','Combined','Production')][string]$Mode='Combined', [string]$RunName='combined', [switch]$Smoke, [switch]$RuntimeDisabled)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$game = 'C:/Games/Sea_Power_8_3'
$helper = Join-Path $game 'BepInEx/plugins/LaunchCandidate.dll'
$backupDirectory = Join-Path $workspace ('SeaPower.Mod/o/live-rollback/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$backup = Join-Path $backupDirectory 'LaunchCandidate.dll'
$originalHash = (Get-FileHash -LiteralPath $helper).Hash
$output = Join-Path $PSScriptRoot $RunName
if (@(Get-Process -Name 'Sea Power' -ErrorAction SilentlyContinue).Count) {
    throw 'Existing game session; refusing regression run'
}
# Isolate the old acceptance plugin without changing any game-owned assembly.
Move-Item -LiteralPath $helper -Destination $backup
try {
    $parameters = @{
        GameRoot = $game
        Mode = $Mode
        OutputDirectory = $output
        ApproveLoaderChanges = $true
        Smoke = $Smoke
        RuntimeDisabled = $RuntimeDisabled
        Cli = (Join-Path $workspace 'SeaPower.Mod/o/windows-msvc-release/_deps/nc_2ce4f16e7409-build/app/naval-cli.exe')
    }
    & "$workspace/SeaPower.Mod/scripts/Start-Live.ps1" @parameters
}
finally {
    Move-Item -LiteralPath $backup -Destination $helper
    $restoredHash = (Get-FileHash -LiteralPath $helper).Hash
    if ($restoredHash -ne $originalHash) {
        throw 'Legacy plugin rollback hash mismatch'
    }
    [ordered]@{ originalHash=$originalHash; restoredHash=$restoredHash; mode=$Mode } |
        ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot "$RunName-legacy-rollback.json")
}
