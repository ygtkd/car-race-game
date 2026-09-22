$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $root 'Tests/CoreTests.csproj') --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
& dotnet build (Join-Path $root 'Server/CoastRacer.Server.csproj') --no-restore -o (Join-Path $root 'Temp/ServerVerify')
if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' | ForEach-Object {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) { throw ($parseErrors | Out-String) }
}
Write-Host 'PASS: shared driving tests, server build, deployment script syntax.'
Write-Host 'Unity Web builds, browser tests and actual devices require separate verification.'