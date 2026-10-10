$ErrorActionPreference = 'Stop'
$baseline = Get-Content Docs/Evidence/Task002/binary-baseline.json -Raw | ConvertFrom-Json
$mismatches = @()
foreach ($entry in $baseline) {
    if (!(Test-Path -LiteralPath $entry.path) -or (Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256) {
        $mismatches += $entry.path
    }
}
$repositories = @('Domain.Api','Gateway.Api','Gateway.Grpc.Api','Gateway.Grpc','SeaPower.Client','SeaPower.Mod','Build.CMake','Docs')
$generatedOutsideOutput = @()
$sourceHeads = @{}
foreach ($repository in $repositories) {
    $sourceHeads[$repository] = (git -C $repository rev-parse HEAD)
    $strays = @(rg --files --hidden --no-ignore -g '!**/o/**' -g '!**/.git/**' -g '*.dll' -g '*.exe' -g '*.pdb' -g 'project.assets.json' -g 'CMakeCache.txt' $repository)
    $generatedOutsideOutput += $strays
}
$ownedPluginFolders = @('NavalCommand.Runtime','NavalCommand.Harness','NavalCommand.Fixture') | Where-Object {
    Test-Path -LiteralPath "C:/Games/Sea_Power_8_3/BepInEx/plugins/$_"
}
$ownedPids = @(Get-ChildItem Docs/Evidence/Task006 -Filter cleanup.json -Recurse | ForEach-Object {
    (Get-Content $_.FullName -Raw | ConvertFrom-Json).ownedPid
})
$leftoverOwnedProcesses = @(Get-Process -ErrorAction SilentlyContinue | Where-Object Id -in $ownedPids | ForEach-Object Id)
$ownerStylePath = 'Docs/CodeStyle.xml'
$ownerStyleUnchanged = !(git -C Docs diff --name-only HEAD -- CodeStyle.xml)
$audit = @{
    VendorFilesChecked = $baseline.Count
    VendorMismatches = $mismatches
    GeneratedOutsideOutput = $generatedOutsideOutput
    RemainingOwnedPluginFolders = @($ownedPluginFolders)
    RemainingOwnedProcesses = $leftoverOwnedProcesses
    DoorstopSha256 = (Get-FileHash 'C:/Games/Sea_Power_8_3/doorstop_config.ini').Hash
    GameAssemblySha256 = (Get-FileHash 'C:/Games/Sea_Power_8_3/Sea Power_Data/Managed/Seapower-Scripts.dll').Hash
    OwnerStyleUnchanged = $ownerStyleUnchanged
    OwnerStyleSha256 = (Get-FileHash $ownerStylePath).Hash
    SourceHeadsBeforeTaskCommits = $sourceHeads
    OwnerEditExcluded = 'Gateway.Api/app/NavalCommand/Gateway/Api/SchemaRoundtrip.cpp'
}
$audit | ConvertTo-Json -Depth 8 | Set-Content Docs/Evidence/Task006/Stabilization/safety-audit.json
if ($mismatches.Count -or $generatedOutsideOutput.Count -or $ownedPluginFolders.Count -or $leftoverOwnedProcesses.Count -or !$ownerStyleUnchanged) {
    throw 'Safety/output audit failed; inspect receipt'
}
