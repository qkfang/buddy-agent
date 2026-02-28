# Buddy Agent Web Application

This is a .NET 8.0 web application that combines both a website and API functionality in a single app.

## Features

- ASP.NET Core Web API with minimal APIs
- Swagger/OpenAPI documentation
- Weather forecast sample API endpoint
- Health check endpoint
- Ready for deployment to Azure App Service

## Prerequisites

- .NET 8.0 SDK or later
- (Optional) Azure CLI for deployment

## Getting Started

### Local Development

1. Restore dependencies:
   ```bash
   dotnet restore
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Access the application:
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger

### Building

```bash
dotnet build
```

### Publishing

```bash
dotnet publish -c Release -o ./publish
```

## API Endpoints

- `GET /weatherforecast` - Returns a sample weather forecast
- `GET /health` - Health check endpoint

## Configuration

Application settings can be configured in:
- `appsettings.json` - General settings
- `appsettings.Development.json` - Development-specific settings

## Deployment

### Deploy to Azure

The application is designed to be deployed to Azure App Service using the Bicep templates in the `/bicep` folder.

1. Deploy infrastructure:
   ```bash
   cd ../../bicep
   ./deploy.sh
   ```

2. Deploy the application:
   ```bash
   cd ../apps/web
   dotnet publish -c Release -o ./publish

   # Using Azure CLI
   az webapp deployment source config-zip \
     --resource-group buddy-agent-rg \
     --name <your-webapp-name> \
     --src ./publish.zip
   ```

## Project Structure

```
apps/web/
├── Program.cs              # Main application entry point and endpoint definitions
├── BuddyAgent.Web.csproj  # Project file with dependencies
├── appsettings.json       # Application configuration
├── Properties/            # Launch settings
└── README.md             # This file
```

## Technology Stack

- .NET 8.0
- ASP.NET Core Minimal APIs
- Swagger/OpenAPI (Swashbuckle)

## License

[Your License Here]
