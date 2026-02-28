#Requires -Version 7
param(
    [Parameter(Mandatory)]
    [string]$WebAppName,
    [string]$ResourceGroup = "rg-buddyagent",
    [string]$ProjectPath   = "$PSScriptRoot/../apps/web/BuddyAgent.Web.csproj",
    [string]$PublishDir    = "$PSScriptRoot/../publish"
)

$ErrorActionPreference = 'Stop'

dotnet publish $ProjectPath -c Release -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for '$ProjectPath'." }

$zipPath = "$PSScriptRoot/../publish.zip"
Compress-Archive -Path "$PublishDir/*" -DestinationPath $zipPath -Force

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipPath `
    --type zip `
    --async false
if ($LASTEXITCODE -ne 0) { throw "Deployment to App Service '$WebAppName' failed." }

$url = az webapp show --resource-group $ResourceGroup --name $WebAppName `
           --query defaultHostName -o tsv
Write-Host "Deployment complete. Checking health endpoint..."

try {
    $response = Invoke-RestMethod -Uri "https://$url/health" -Method Get
    if ($response.status -eq "healthy") {
        Write-Host "Health check passed: $($response.status)"
    } else {
        throw "Health check returned unexpected status: $($response.status)"
    }
} catch {
    throw "Health check failed for 'https://$url/health': $_"
}

Remove-Item $zipPath, $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
