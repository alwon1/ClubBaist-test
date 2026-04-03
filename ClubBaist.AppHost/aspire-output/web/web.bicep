@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param app_service_env_outputs_azure_container_registry_endpoint string

param app_service_env_outputs_planid string

param app_service_env_outputs_azure_container_registry_managed_identity_id string

param app_service_env_outputs_azure_container_registry_managed_identity_client_id string

param web_containerimage string

param web_containerport string

param sql_outputs_sqlserverfqdn string

param web_identity_outputs_id string

param web_identity_outputs_clientid string

param app_service_env_outputs_azure_app_service_dashboard_uri string

param app_service_env_outputs_azure_website_contributor_managed_identity_id string

param app_service_env_outputs_azure_website_contributor_managed_identity_principal_id string

resource mainContainer 'Microsoft.Web/sites/sitecontainers@2025-03-01' = {
  name: 'main'
  properties: {
    authType: 'UserAssigned'
    image: web_containerimage
    isMain: true
    targetPort: web_containerport
    userManagedIdentityClientId: app_service_env_outputs_azure_container_registry_managed_identity_client_id
  }
  parent: webapp
}

resource webapp 'Microsoft.Web/sites@2025-03-01' = {
  name: take('${toLower('web')}-${uniqueString(resourceGroup().id)}', 60)
  location: location
  properties: {
    serverFarmId: app_service_env_outputs_planid
    keyVaultReferenceIdentity: web_identity_outputs_id
    siteConfig: {
      numberOfWorkers: 30
      linuxFxVersion: 'SITECONTAINERS'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: app_service_env_outputs_azure_container_registry_managed_identity_client_id
      appSettings: [
        {
          name: 'WEBSITES_PORT'
          value: web_containerport
        }
        {
          name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
          value: 'in_memory'
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'HTTP_PORTS'
          value: web_containerport
        }
        {
          name: 'ConnectionStrings__clubbaist'
          value: 'Server=tcp:${sql_outputs_sqlserverfqdn},1433;Encrypt=True;Authentication="Active Directory Default";Database=clubbaist'
        }
        {
          name: 'CLUBBAIST_HOST'
          value: sql_outputs_sqlserverfqdn
        }
        {
          name: 'CLUBBAIST_PORT'
          value: '1433'
        }
        {
          name: 'CLUBBAIST_URI'
          value: 'mssql://${sql_outputs_sqlserverfqdn}:1433/clubbaist'
        }
        {
          name: 'CLUBBAIST_JDBCCONNECTIONSTRING'
          value: 'jdbc:sqlserver://${sql_outputs_sqlserverfqdn}:1433;database=clubbaist;encrypt=true;trustServerCertificate=false'
        }
        {
          name: 'CLUBBAIST_DATABASENAME'
          value: 'clubbaist'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: web_identity_outputs_clientid
        }
        {
          name: 'AZURE_TOKEN_CREDENTIALS'
          value: 'ManagedIdentityCredential'
        }
        {
          name: 'ASPIRE_ENVIRONMENT_NAME'
          value: 'app-service-env'
        }
        {
          name: 'OTEL_SERVICE_NAME'
          value: 'web'
        }
        {
          name: 'OTEL_EXPORTER_OTLP_PROTOCOL'
          value: 'grpc'
        }
        {
          name: 'OTEL_EXPORTER_OTLP_ENDPOINT'
          value: 'http://localhost:6001'
        }
        {
          name: 'WEBSITE_ENABLE_ASPIRE_OTEL_SIDECAR'
          value: 'true'
        }
        {
          name: 'OTEL_COLLECTOR_URL'
          value: app_service_env_outputs_azure_app_service_dashboard_uri
        }
        {
          name: 'OTEL_CLIENT_ID'
          value: app_service_env_outputs_azure_container_registry_managed_identity_client_id
        }
      ]
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${app_service_env_outputs_azure_container_registry_managed_identity_id}': { }
      '${web_identity_outputs_id}': { }
    }
  }
}

resource web_website_ra 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webapp.id, app_service_env_outputs_azure_website_contributor_managed_identity_id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772'))
  properties: {
    principalId: app_service_env_outputs_azure_website_contributor_managed_identity_principal_id
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772')
    principalType: 'ServicePrincipal'
  }
  scope: webapp
}

resource slotConfigNames 'Microsoft.Web/sites/config@2025-03-01' = {
  name: 'slotConfigNames'
  properties: {
    appSettingNames: [
      'OTEL_SERVICE_NAME'
    ]
  }
  parent: webapp
}