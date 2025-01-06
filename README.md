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
   
   # Create an Azure AD application
   APP_ID=$(az ad app create --display-name "GitHub-Actions-App" --query appId -o tsv)
   
   # Create a service principal
   az ad sp create --id $APP_ID
   
   # Get subscription ID and tenant ID
   SUBSCRIPTION_ID=$(az account show --query id -o tsv)
   TENANT_ID=$(az account show --query tenantId -o tsv)
   
   # Assign contributor role
   az role assignment create \
     --role contributor \
     --subscription $SUBSCRIPTION_ID \
     --assignee-object-id $(az ad sp show --id $APP_ID --query id -o tsv) \
     --assignee-principal-type ServicePrincipal
   
   # Configure federated credentials
   az ad app federated-credential create \
     --id $APP_ID \
     --parameters "{
       'name': 'github-federated',
       'issuer': 'https://token.actions.githubusercontent.com',
       'subject': 'repo:yourusername/URLShortener:ref:refs/heads/master',
       'audiences': ['api://AzureADTokenExchange']
     }"
   
   # Create a resource group
   az group create --name urlshortener-rg --location westeurope
   
   # Deploy infrastructure using Bicep
   az deployment group create \
     --resource-group urlshortener-rg \
     --template-file infrastructure/main.bicep \
     --parameters name=urlshortener
   
   echo "Add these secrets to your GitHub repository:"
   echo "AZURE_CLIENT_ID: $APP_ID"
   echo "AZURE_TENANT_ID: $TENANT_ID"
   echo "AZURE_SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
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
