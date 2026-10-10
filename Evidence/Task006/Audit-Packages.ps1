param([Parameter(Mandatory)][ValidateSet('Debug','Release')][string]$Configuration)
$ErrorActionPreference = 'Stop'
$packageRecords = @()
foreach ($kind in @('package','harness')) {
    $stage = (Resolve-Path "SeaPower.Mod/o/$kind/0.1.0").Path
    $manifest = Get-Content "$stage/manifest.json" -Raw | ConvertFrom-Json
    $actualFiles = @(Get-ChildItem $stage -Recurse -File | Where-Object Name -ne 'manifest.json')
    if ($actualFiles.Count -ne $manifest.Count) {
        throw 'Package inventory mismatch'
    }
    foreach ($entry in $manifest) {
        $path = Join-Path $stage $entry.file
        if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $entry.sha256) {
            throw "Package hash mismatch: $path"
        }
    }
    $binaries = @($actualFiles | Where-Object Extension -eq '.dll' | ForEach-Object Name | Sort-Object)
    $expectedBinaries = if ($kind -eq 'package') {
        @('Gateway.Api.dll','Google.Protobuf.dll','SeaPower.Client.dll','SeaPower.Host.dll','SeaPower.Mod.dll')
    }
    else {
        @('SeaPower.Harness.dll')
    }
    if (Compare-Object $expectedBinaries $binaries) {
        throw 'Package binary allowlist mismatch'
    }
    if ($kind -eq 'package') {
        $config = Get-Content "$stage/config.json" -Raw | ConvertFrom-Json
        if ($config.enabledByDefault -or $config.clientAutoLaunch) {
            throw 'Production package unexpectedly enabled'
        }
    }
    $packageRecords += @{ Kind = $kind; Configuration = $Configuration; Inventory = $manifest; HashesVerified = $true; BinaryAllowlist = $binaries }
}
$packageRecords | ConvertTo-Json -Depth 8 | Set-Content "Docs/Evidence/Task006/packages-$Configuration.json"
