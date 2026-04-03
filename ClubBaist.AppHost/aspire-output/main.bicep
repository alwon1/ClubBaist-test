targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module app_service_env_acr 'app-service-env-acr/app-service-env-acr.bicep' = {
  name: 'app-service-env-acr'
  scope: rg
  params: {
    location: location
  }
}

module app_service_env 'app-service-env/app-service-env.bicep' = {
  name: 'app-service-env'
  scope: rg
  params: {
    location: location
    app_service_env_acr_outputs_name: app_service_env_acr.outputs.name
    userPrincipalId: principalId
  }
}

module sql 'sql/sql.bicep' = {
  name: 'sql'
  scope: rg
  params: {
    location: location
  }
}

module seeder_identity 'seeder-identity/seeder-identity.bicep' = {
  name: 'seeder-identity'
  scope: rg
  params: {
    location: location
  }
}

module seeder_roles_sql 'seeder-roles-sql/seeder-roles-sql.bicep' = {
  name: 'seeder-roles-sql'
  scope: rg
  params: {
    location: location
    sql_outputs_name: sql.outputs.name
    sql_outputs_sqlserveradminname: sql.outputs.sqlServerAdminName
    principalId: seeder_identity.outputs.principalId
    principalName: seeder_identity.outputs.principalName
  }
}

module web_identity 'web-identity/web-identity.bicep' = {
  name: 'web-identity'
  scope: rg
  params: {
    location: location
  }
}

module web_roles_sql 'web-roles-sql/web-roles-sql.bicep' = {
  name: 'web-roles-sql'
  scope: rg
  params: {
    location: location
    sql_outputs_name: sql.outputs.name
    sql_outputs_sqlserveradminname: sql.outputs.sqlServerAdminName
    principalId: web_identity.outputs.principalId
    principalName: web_identity.outputs.principalName
  }
}

output app_service_env_AZURE_CONTAINER_REGISTRY_ENDPOINT string = app_service_env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT

output app_service_env_planId string = app_service_env.outputs.planId

output app_service_env_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = app_service_env.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID

output app_service_env_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_CLIENT_ID string = app_service_env.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_CLIENT_ID

output sql_sqlServerFqdn string = sql.outputs.sqlServerFqdn

output seeder_identity_id string = seeder_identity.outputs.id

output seeder_identity_clientId string = seeder_identity.outputs.clientId

output app_service_env_AZURE_APP_SERVICE_DASHBOARD_URI string = app_service_env.outputs.AZURE_APP_SERVICE_DASHBOARD_URI

output app_service_env_AZURE_WEBSITE_CONTRIBUTOR_MANAGED_IDENTITY_ID string = app_service_env.outputs.AZURE_WEBSITE_CONTRIBUTOR_MANAGED_IDENTITY_ID

output app_service_env_AZURE_WEBSITE_CONTRIBUTOR_MANAGED_IDENTITY_PRINCIPAL_ID string = app_service_env.outputs.AZURE_WEBSITE_CONTRIBUTOR_MANAGED_IDENTITY_PRINCIPAL_ID

output web_identity_id string = web_identity.outputs.id

output web_identity_clientId string = web_identity.outputs.clientId