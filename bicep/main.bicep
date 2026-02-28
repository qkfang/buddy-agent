// Main Bicep template for deploying .NET Web App
@description('The name of the web app')
param webAppName string = 'buddy-agent-webapp-${uniqueString(resourceGroup().id)}'

@description('The location for all resources')
param location string = resourceGroup().location

@description('The SKU of the App Service Plan')
@allowed([
  'F1'
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1v2'
  'P2v2'
  'P3v2'
])
param appServicePlanSku string = 'B1'

@description('The .NET Framework version')
param dotnetVersion string = 'v8.0'

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${webAppName}-plan'
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// Web App
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
      ]
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      http20Enabled: true
    }
    httpsOnly: true
  }
}

// Output values
output webAppUrl string = webApp.properties.defaultHostName
output webAppName string = webApp.name
output appServicePlanName string = appServicePlan.name
