$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
if (-not (Test-Path 'WebBuild/play/Build/play.wasm')) { throw 'Committed Unity Web build is missing.' }
& dotnet restore Server/CoastRacer.Server.csproj --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& dotnet publish Server/CoastRacer.Server.csproj -c Release -o Builds/ci-server -p:UseAppHost=false --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
New-Item -ItemType Directory -Force Builds/ci-server/wwwroot | Out-Null
Copy-Item WebBuild/* Builds/ci-server/wwwroot -Recurse -Force
Compress-Archive -Path Builds/ci-server/* -DestinationPath Builds/coast-racer-appservice.zip -Force