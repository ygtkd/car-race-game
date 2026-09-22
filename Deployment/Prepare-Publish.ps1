param([string]$OutputPath = (Join-Path $PSScriptRoot '../Builds/server'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$site = Join-Path $root 'Builds/site'
if (-not (Test-Path (Join-Path $site 'play/Build/play.wasm'))) { throw 'Build the Unity Web game first.' }
$node = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/node/node.exe'
& $node (Join-Path $PSScriptRoot 'Compress-Web.mjs') $site
if ($LASTEXITCODE -ne 0) { throw 'Compression failed.' }
& dotnet publish (Join-Path $root 'Server/CoastRacer.Server.csproj') -c Release -o $OutputPath -p:UseAppHost=false --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Server publish failed.' }
New-Item -ItemType Directory -Force -Path (Join-Path $OutputPath 'wwwroot') | Out-Null
Copy-Item -Path (Join-Path $site '*') -Destination (Join-Path $OutputPath 'wwwroot') -Recurse -Force
$zip = Join-Path $root 'Builds/coast-racer-appservice.zip'
Compress-Archive -Path (Join-Path $OutputPath '*') -DestinationPath $zip -Force
Write-Host ('Prepared locally: ' + $zip)
Write-Host 'No Azure resources were created or modified.'
