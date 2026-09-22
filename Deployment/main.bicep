targetScope = 'resourceGroup'
@description('地域でFreeプランとSQL無料オファーを利用できることを公開前に確認する。')
param location string = resourceGroup().location
@minLength(3)
@maxLength(40)
param appName string
param sqlServerName string
param databaseName string = 'coastracer'
param sqlAdminName string
param sqlAdminObjectId string

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-free'
  location: location
  kind: 'linux'
  sku: { name: 'F1', tier: 'Free', capacity: 1 }
  properties: { reserved: true }
}
resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: 'dotnet CoastRacer.Server.dll'
      alwaysOn: false
      webSocketsEnabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Racer__MaxPlayers', value: '2' }
        { name: 'ConnectionStrings__RacerSql', value: 'Server=tcp:${sqlServerName}.${environment().suffixes.sqlServerHostname},1433;Database=${databaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Pooling=False;Connect Timeout=10;ConnectRetryCount=0;' }
      ]
    }
  }
}
resource sql 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: sqlAdminName
      sid: sqlAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}
resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sql
  name: databaseName
  location: location
  sku: { name: 'GP_S_Gen5', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}
// No firewall wildcard is created. Add only the web app outbound addresses and an administrator's IP after provisioning.
output url string = 'https://${app.properties.defaultHostName}'
output managedIdentityObjectId string = app.identity.principalId
output outboundAddresses string = app.properties.outboundIpAddresses
output sqlHost string = '${sql.name}.${environment().suffixes.sqlServerHostname}'
