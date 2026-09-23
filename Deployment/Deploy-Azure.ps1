param(
 [Parameter(Mandatory=$true)][string]$SubscriptionId,
 [Parameter(Mandatory=$true)][string]$ResourceGroup,
 [Parameter(Mandatory=$true)][string]$AppName,
 [Parameter(Mandatory=$true)][string]$SqlServer,
 [string]$DatabaseName='coastracer',
 [switch]$Publish
)
$ErrorActionPreference='Stop'
if(-not(Get-Command az -ErrorAction SilentlyContinue)){throw 'Install Azure CLI and run az login first.'}
function Read-AzJson {
 param([string[]]$Arguments)
 $value=& az @Arguments --subscription $SubscriptionId -o json
 if($LASTEXITCODE -ne 0){throw 'Azure inspection failed. No deployment performed.'}
 return ($value | ConvertFrom-Json)
}
$app=Read-AzJson @('webapp','show','--resource-group',$ResourceGroup,'--name',$AppName)
$plan=Read-AzJson @('appservice','plan','show','--ids',$app.serverFarmId)
if($plan.sku.name -ne 'F1'){throw 'Only the Free F1 App Service plan is permitted.'}
$dbId='/subscriptions/'+$SubscriptionId+'/resourceGroups/'+$ResourceGroup+'/providers/Microsoft.Sql/servers/'+$SqlServer+'/databases/'+$DatabaseName
$db=Read-AzJson @('resource','show','--ids',$dbId,'--api-version','2023-08-01')
if($db.properties.useFreeLimit -ne $true -or $db.properties.freeLimitExhaustionBehavior -ne 'AutoPause'){throw 'SQL free offer with AutoPause is required. No deployment performed.'}
$config=Read-AzJson @('webapp','config','show','--resource-group',$ResourceGroup,'--name',$AppName)
Write-Host ("Runtime={0}, WebSockets={1}, AlwaysOn={2}" -f $config.linuxFxVersion,$config.webSocketsEnabled,$config.alwaysOn)
if($Publish){
 if($plan.properties.reserved -ne $true -and $plan.reserved -ne $true){throw 'Expected a Linux App Service plan.'}
 & az webapp config set --subscription $SubscriptionId --resource-group $ResourceGroup --name $AppName --linux-fx-version 'DOTNETCORE|10.0' --web-sockets-enabled true --always-on false --startup-file 'dotnet CoastRacer.Server.dll' --output none
 if($LASTEXITCODE -ne 0){throw 'App Service configuration update failed.'}
 $config=Read-AzJson @('webapp','config','show','--resource-group',$ResourceGroup,'--name',$AppName)
}
if($config.linuxFxVersion -ne 'DOTNETCORE|10.0' -or $config.alwaysOn -eq $true -or $config.webSocketsEnabled -ne $true){throw 'Expected .NET 10 Linux, WebSockets enabled and Always On disabled.'}
Write-Host 'Free-tier resource configuration verified.'
if(-not $Publish){Write-Host 'Read-only check complete. Use -Publish only when deployment is authorized.';return}
$zip=Join-Path $PSScriptRoot '../Builds/coast-racer-appservice.zip'
if(-not(Test-Path -LiteralPath $zip)){throw 'Run Prepare-Publish.ps1 first.'}
& az webapp config appsettings set --subscription $SubscriptionId --resource-group $ResourceGroup --name $AppName --settings 'Racer__MaxPlayers=8' --output none
if($LASTEXITCODE -ne 0){throw 'Player capacity configuration failed.'}
& az webapp identity assign --subscription $SubscriptionId --resource-group $ResourceGroup --name $AppName --output none
if($LASTEXITCODE -ne 0){throw 'Managed identity setup failed.'}
& az webapp deploy --subscription $SubscriptionId --resource-group $ResourceGroup --name $AppName --src-path $zip --type zip --track-status false
if($LASTEXITCODE -ne 0){throw 'Deployment failed.'}
Write-Host ('https://'+$app.defaultHostName)
