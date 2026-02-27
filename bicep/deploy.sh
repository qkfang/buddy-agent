#!/bin/bash

# Deployment script for Buddy Agent Azure infrastructure
set -e

# Configuration
RESOURCE_GROUP="buddy-agent-rg"
LOCATION="eastus"
TEMPLATE_FILE="main.bicep"
PARAMETERS_FILE="parameters.json"

echo "🚀 Starting deployment of Buddy Agent infrastructure..."

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI is not installed. Please install it first."
    exit 1
fi

# Check if user is logged in
echo "📋 Checking Azure login status..."
if ! az account show &> /dev/null; then
    echo "❌ Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

# Display current subscription
SUBSCRIPTION=$(az account show --query name -o tsv)
echo "✅ Using subscription: $SUBSCRIPTION"

# Create resource group if it doesn't exist
echo "📦 Creating resource group: $RESOURCE_GROUP in $LOCATION..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none

# Deploy the template
echo "🔧 Deploying Bicep template..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file $TEMPLATE_FILE \
  --parameters $PARAMETERS_FILE \
  --query properties.outputs \
  --output json)

# Display outputs
echo ""
echo "✅ Deployment completed successfully!"
echo ""
echo "📊 Deployment outputs:"
echo "$DEPLOYMENT_OUTPUT" | jq -r 'to_entries[] | "  \(.key): \(.value.value)"'
echo ""
echo "🌐 Web App URL: https://$(echo $DEPLOYMENT_OUTPUT | jq -r '.webAppUrl.value')"
echo ""
echo "🎉 Done!"
