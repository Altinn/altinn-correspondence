@secure()
param namePrefix string
param location string

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-app-identity'
  location: location
}

var storageBlobDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentity.id, storageBlobDataContributorRoleId)
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

var azureDefenderForStorageScannerOperatorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0f641de8-0b88-4198-bdef-bd8b45ceba96')
resource azureDefenderForStorageScannerOperatorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentity.id, azureDefenderForStorageScannerOperatorRoleId)
  properties: {
    roleDefinitionId: azureDefenderForStorageScannerOperatorRoleId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
output id string = userAssignedIdentity.id
output clientId string = userAssignedIdentity.properties.clientId
output principalId string = userAssignedIdentity.properties.principalId
output tenantId string = userAssignedIdentity.properties.tenantId
output name string = userAssignedIdentity.name
