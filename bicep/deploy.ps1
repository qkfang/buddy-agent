#Requires -Version 7
param(
    [string]$ResourceGroup = "rg-buddyagent",
    [string]$Location      = "eastus",
    [string]$TemplateFile  = "$PSScriptRoot/main.bicep",
    [string]$ParametersFile = "$PSScriptRoot/parameters.json",
    [SecureString]$SqlAdminPassword
)

$ErrorActionPreference = 'Stop'

if (-not $SqlAdminPassword) {
    $SqlAdminPassword = Read-Host "SQL admin password" -AsSecureString
}
$sqlPwd = [System.Net.NetworkCredential]::new("", $SqlAdminPassword).Password

az group create --name $ResourceGroup --location $Location --output none

$output = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters $ParametersFile `
    --parameters sqlAdminPassword=$sqlPwd `
    --query properties.outputs `
    --output json | ConvertFrom-Json

$sqlPwd = $null

if ($LASTEXITCODE -ne 0) { throw "Deployment failed." }

Write-Host "Deployment succeeded."
Write-Host "Web App URL : https://$($output.webAppUrl.value)"
Write-Host "SQL Server  : $($output.sqlServerFqdn.value)"

Write-Host "Verifying resources..."
$webAppName  = $output.webAppName.value
$sqlSrvName  = ($output.sqlServerFqdn.value -split '\.')[0]

$webApp = az webapp show --resource-group $ResourceGroup --name $webAppName --query name -o tsv
if ($LASTEXITCODE -ne 0) { throw "App Service '$webAppName' not found in '$ResourceGroup'." }

$sqlDb = az sql db show --resource-group $ResourceGroup --server $sqlSrvName `
             --name BuddyAgentDb --query name -o tsv
if ($LASTEXITCODE -ne 0) { throw "SQL Database 'BuddyAgentDb' not found on server '$sqlSrvName'." }

Write-Host "All required resources are present: App Service '$webApp', SQL DB '$sqlDb'."
