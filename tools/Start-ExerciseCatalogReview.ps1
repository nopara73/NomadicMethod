param([int]$Port = 4173)

$ErrorActionPreference = 'Stop'
$serverPath = Join-Path $PSScriptRoot 'catalog-review\server.mjs'
$server = Start-Process node `
    -ArgumentList @($serverPath, '--port', $Port) `
    -PassThru `
    -WindowStyle Hidden

try {
    Start-Sleep -Milliseconds 700
    Start-Process "http://127.0.0.1:$Port"
    Write-Host "Nomadic Method exercise review is open. Close this window or press Ctrl+C to stop it."
    Wait-Process -Id $server.Id
}
finally {
    if (-not $server.HasExited) {
        Stop-Process -Id $server.Id
    }
}
