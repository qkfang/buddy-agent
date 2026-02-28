#Requires -Version 7
param(
    [string]$ResourceGroup  = "rg-buddyagent",
    [string]$Location       = "australiaeast",
    [string]$TemplateFile   = "$PSScriptRoot/main.bicep",
    [string]$ParametersFile = "$PSScriptRoot/parameters.json",
    [bool]$DeploySql        = $false,
    [SecureString]$SqlAdminPassword
)

$ErrorActionPreference = 'Stop'

$extraParams = @("deploySql=$($DeploySql.ToString().ToLower())")

if ($DeploySql) {
    if (-not $SqlAdminPassword) {
        $SqlAdminPassword = Read-Host "SQL admin password" -AsSecureString
    }
    $sqlPwd = [System.Net.NetworkCredential]::new("", $SqlAdminPassword).Password
    $extraParams += "sqlAdminPassword=$sqlPwd"
}

az group create --name $ResourceGroup --location $Location --output none

$output = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters $ParametersFile `
    @extraParams `
    --query properties.outputs `
    --output json | ConvertFrom-Json

if ($DeploySql) { $sqlPwd = $null }

if ($LASTEXITCODE -ne 0) { throw "Deployment failed." }

Write-Host "Deployment succeeded."
Write-Host "Web App URL : https://$($output.webAppUrl.value)"

Write-Host "Verifying resources..."
$webAppName = $output.webAppName.value

$webApp = az webapp show --resource-group $ResourceGroup --name $webAppName --query name -o tsv
if ($LASTEXITCODE -ne 0) { throw "App Service '$webAppName' not found in '$ResourceGroup'." }
Write-Host "App Service present: $webApp"

if ($DeploySql) {
    $sqlSrvName = ($output.sqlServerFqdn.value -split '\.')[0]
    $sqlDb = az sql db show --resource-group $ResourceGroup --server $sqlSrvName `
                 --name BuddyAgentDb --query name -o tsv
    if ($LASTEXITCODE -ne 0) { throw "SQL Database 'BuddyAgentDb' not found on server '$sqlSrvName'." }
    Write-Host "SQL Database present: $sqlDb"
}

Write-Host "All required resources verified."
