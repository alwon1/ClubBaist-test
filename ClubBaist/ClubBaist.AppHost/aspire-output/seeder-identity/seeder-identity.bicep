@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource seeder_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('seeder_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = seeder_identity.id

output clientId string = seeder_identity.properties.clientId

output principalId string = seeder_identity.properties.principalId

output principalName string = seeder_identity.name

output name string = seeder_identity.name