# buddy-agent

A cloud-native .NET web application with Azure infrastructure as code.

## Project Structure

```
buddy-agent/
├── bicep/                          # Azure Infrastructure as Code
│   ├── main.bicep                  # Main Bicep template for Azure resources
│   ├── parameters.json             # Deployment parameters
│   ├── deploy.sh                   # Automated deployment script
│   └── README.md                   # Infrastructure documentation
└── apps/
    └── web/                        # .NET Web Application & API
        ├── Program.cs              # Application entry point
        ├── BuddyAgent.Web.csproj  # Project configuration
        ├── appsettings.json        # App settings
        └── README.md               # Web app documentation
```

## Features

### Infrastructure (Bicep)
- Azure App Service Plan (Linux)
- Azure Web App configured for .NET 8.0
- Automated deployment scripts
- Production-ready configuration with HTTPS and security best practices

### Web Application
- ASP.NET Core 8.0 Minimal APIs
- Combined website and API functionality
- Swagger/OpenAPI documentation
- Health check endpoint
- Sample weather forecast API

## Getting Started

### Prerequisites
- .NET 8.0 SDK or later
- Azure CLI (for deployment)
- Azure subscription

### Local Development

1. Navigate to the web app directory:
   ```bash
   cd apps/web
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Access the application:
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger

### Deploy to Azure

1. Deploy the infrastructure:
   ```bash
   cd bicep
   ./deploy.sh
   ```

2. Deploy the application:
   ```bash
   cd ../apps/web
   dotnet publish -c Release -o ./publish

   # Create deployment package
   cd publish
   zip -r ../publish.zip .
   cd ..

   # Deploy to Azure
   az webapp deployment source config-zip \
     --resource-group buddy-agent-rg \
     --name <your-webapp-name> \
     --src publish.zip
   ```

## Documentation

- [Infrastructure Setup](bicep/README.md) - Details about Azure infrastructure
- [Web Application](apps/web/README.md) - Web app development guide

## Technology Stack

- **.NET 8.0** - Modern web framework
- **Azure Bicep** - Infrastructure as Code
- **Azure App Service** - Hosting platform
- **Swagger/OpenAPI** - API documentation

## Contributing

[Add your contribution guidelines here]

## License

[Add your license information here]
