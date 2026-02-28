# Azure Infrastructure as Code (Bicep)

This directory contains Bicep templates for deploying the Buddy Agent infrastructure to Azure.

## Overview

The infrastructure includes:
- Azure App Service Plan (Linux)
- Azure Web App configured for .NET 9.0
- Azure SQL Server (DTU-based)
- Azure SQL Database (`BuddyAgentDb`, Basic tier by default)

## Files

- `main.bicep` - Main Bicep template defining all Azure resources
- `parameters.json` - Parameter file with default values
- `deploy.sh` - Deployment script for easy deployment

## Prerequisites

- Azure CLI installed and authenticated
- Azure subscription with appropriate permissions
- Bicep CLI (automatically installed with Azure CLI 2.20.0+)
- An Azure Key Vault storing the SQL admin password as secret `SqlAdminPassword`

## Deployment

### Using Azure CLI

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "your-subscription-id"

# Create a resource group
az group create --name buddy-agent-rg --location eastus

# Update parameters.json – replace the Key Vault placeholders:
#   <subscription-id>  →  your Azure subscription ID
#   <resource-group>   →  the resource group containing your Key Vault
#   <vault-name>       →  the name of your Key Vault

# Deploy the template
az deployment group create \
  --resource-group buddy-agent-rg \
  --template-file main.bicep \
  --parameters parameters.json
```

### Using the deployment script

```bash
chmod +x deploy.sh
./deploy.sh
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `webAppName` | auto-generated | Name of the web app |
| `location` | eastus | Azure region |
| `appServicePlanSku` | B1 | App Service Plan SKU |
| `dotnetVersion` | v9.0 | .NET runtime version |
| `sqlAdminLogin` | buddyagentadmin | SQL Server admin login |
| `sqlAdminPassword` | *(Key Vault secret)* | SQL Server admin password |
| `sqlServerName` | buddy-agent-sql | SQL Server name |
| `sqlDatabaseName` | BuddyAgentDb | SQL Database name |
| `sqlDatabaseSku` | Basic | DTU SKU (Basic / S0 / S1 / S2) |

> **Note:** `sqlAdminPassword` must be supplied via a Key Vault reference (see `parameters.json`).
> Update the placeholder IDs in `parameters.json` before deploying.

## Outputs

After successful deployment, the template outputs:
- `webAppUrl` - The URL of the deployed web app
- `webAppName` - The name of the web app
- `appServicePlanName` - The name of the App Service Plan
- `sqlServerFqdn` - The fully-qualified domain name of the SQL Server
- `sqlDatabaseName` - The name of the SQL Database

## Customization

To customize the deployment, edit `parameters.json` or pass parameters directly via the Azure CLI:

```bash
az deployment group create \
  --resource-group buddy-agent-rg \
  --template-file main.bicep \
  --parameters webAppName=my-custom-name location=westus2 sqlDatabaseSku=S1
```

