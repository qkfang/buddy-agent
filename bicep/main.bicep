// Main Bicep template for deploying .NET Web App + Azure SQL (DTU)
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

@description('Deploy Azure SQL resources (requires policy-compliant subscription)')
param deploySql bool = false

@description('The SQL Server administrator login name')
param sqlAdminLogin string = 'buddyagentadmin'

@description('The SQL Server administrator password')
@secure()
param sqlAdminPassword string = ''

@description('The name of the SQL Server')
param sqlServerName string = 'buddy-agent-sql-${uniqueString(resourceGroup().id)}'

@description('The name of the SQL Database')
param sqlDatabaseName string = 'BuddyAgentDb'

@description('The DTU-based SQL Database SKU')
@allowed([
  'Basic'
  'S0'
  'S1'
  'S2'
])
param sqlDatabaseSku string = 'Basic'

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

// Azure SQL Server (conditional)
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = if (deploySql) {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

// Allow Azure services to connect to SQL Server (conditional)
resource sqlServerFirewallAzure 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = if (deploySql) {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Azure SQL Database - DTU Basic tier (conditional)
resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = if (deploySql) {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: sqlDatabaseSku
    tier: sqlDatabaseSku == 'Basic' ? 'Basic' : 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
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
      linuxFxVersion: 'DOTNETCORE|9.0'
      connectionStrings: deploySql ? [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServer.?properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
          type: 'SQLAzure'
        }
      ] : []
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'GITHUB_TOKEN'
          value: ''
        }
        {
          name: 'GITHUB_MODEL'
          value: 'gpt-4o-mini'
        }
      ]
      alwaysOn: appServicePlanSku != 'F1'
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
output sqlServerFqdn string = deploySql ? sqlServer.?properties.fullyQualifiedDomainName ?? '' : ''
output sqlDatabaseName string = deploySql ? sqlDatabase.?name ?? '' : ''
