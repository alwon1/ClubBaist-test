@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource app_service_env_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: take('appserviceenvacr${uniqueString(resourceGroup().id)}', 50)
  location: location
  sku: {
    name: 'Basic'
  }
  tags: {
    'aspire-resource-name': 'app-service-env-acr'
  }
}

output name string = app_service_env_acr.name

output loginServer string = app_service_env_acr.properties.loginServer