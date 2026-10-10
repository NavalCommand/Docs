param([Parameter(Mandatory)][string]$HostExecutable,
    [Parameter(Mandatory)][string]$Cli,
    [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$hostProcess = Start-Process -FilePath $HostExecutable -ArgumentList '127.0.0.1:50052' -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$OutputDirectory/host.log" -RedirectStandardError "$OutputDirectory/host-error.log"
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (!(Test-Path "$OutputDirectory/host.log") -or !(Select-String "$OutputDirectory/host.log" -Pattern MOCK_READY -Quiet)) {
        if ($hostProcess.HasExited -or [DateTime]::UtcNow -gt $deadline) {
            throw 'Mock readiness failed'
        }
        Start-Sleep -Milliseconds 100
    }
    & $Cli --endpoint 127.0.0.1:50052 --movement-smoke *> "$OutputDirectory/cli.log"
    if ($LASTEXITCODE) {
        throw "Movement CLI failed: $LASTEXITCODE"
    }
}
finally {
    if (!$hostProcess.HasExited) {
        Stop-Process -Id $hostProcess.Id -Force
    }
}
