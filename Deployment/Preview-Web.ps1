param([int]$Port=5080)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:'+$Port
Write-Host ('Open http://localhost:'+ $Port +'/play/ . Ctrl+C stops the server.')
Push-Location (Join-Path $root 'Server')
try { & dotnet run --project CoastRacer.Server.csproj --no-launch-profile } finally { Pop-Location }
