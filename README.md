# URLShortener

A modern URL shortening service built with .NET 8.0.

## 🚀 Features

- URL shortening
- Swagger/OpenAPI documentation
- HTTPS support
- Development and Production configurations
- Azure App Service deployment

## 🛠️ Tech Stack

- .NET 8.0
- ASP.NET Core Web API
- Swagger/OpenAPI
- IIS Express/Kestrel
- Azure Bicep
- Azure App Service

## 🏗️ Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- Azure CLI
- Azure subscription

## 🚦 Getting Started

1. Clone the repository
   ```bash
   git clone https://github.com/yourusername/URLShortener.git
   ```

2. Azure Setup
   ```bash
   # Login to Azure
   az login
   
   # Create a resource group
   az group create --name urlshortener-rg --location westeurope
   
   # Deploy infrastructure using Bicep
   az deployment group create \
     --resource-group urlshortener-rg \
     --template-file infrastructure/main.bicep \
     --parameters name=urlshortener
   ```

3. Local Development
   ```bash
   # Navigate to project directory
   cd URLShortener
   
   # Restore dependencies
   dotnet restore
   
   # Run the application
   dotnet run
   ```

4. Access the application
   - Local: https://localhost:5001
   - Azure: Your App Service URL will be displayed in the Azure portal

## 📝 Infrastructure Details

The application is deployed to Azure App Service using Bicep templates. The infrastructure includes:

- App Service Plan (Basic B1 tier)
- Web App with .NET 8.0 runtime
- HTTPS-only access
- GitHub-based deployment

You can customize the infrastructure by modifying the Bicep files in the `infrastructure` directory.
