# Azure Infrastructure as Code (Bicep)

This directory contains Bicep templates for deploying the Buddy Agent infrastructure to Azure.

## Overview

The infrastructure includes:
- Azure App Service Plan (Linux)
- Azure Web App configured for .NET 8.0

## Files

- `main.bicep` - Main Bicep template defining all Azure resources
- `parameters.json` - Parameter file with default values
- `deploy.sh` - Deployment script for easy deployment

## Prerequisites

- Azure CLI installed and authenticated
- Azure subscription with appropriate permissions
- Bicep CLI (automatically installed with Azure CLI 2.20.0+)

## Deployment

### Using Azure CLI

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "your-subscription-id"

# Create a resource group
az group create --name buddy-agent-rg --location eastus

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

- `webAppName` - Name of the web app (defaults to auto-generated unique name)
- `location` - Azure region for deployment (default: eastus)
- `appServicePlanSku` - SKU for the App Service Plan (default: B1)
- `dotnetVersion` - .NET version to use (default: v8.0)

## Outputs

After successful deployment, the template outputs:
- `webAppUrl` - The URL of the deployed web app
- `webAppName` - The name of the web app
- `appServicePlanName` - The name of the App Service Plan

## Customization

To customize the deployment, edit the `parameters.json` file or pass parameters directly via the Azure CLI:

```bash
az deployment group create \
  --resource-group buddy-agent-rg \
  --template-file main.bicep \
  --parameters webAppName=my-custom-name location=westus2
```
