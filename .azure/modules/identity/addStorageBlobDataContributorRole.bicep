targetScope = 'subscription'
param userAssignedIdentityPrincipalId string

var roleDefinitionResourceId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage blob data contrubotur role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentityPrincipalId, roleDefinitionResourceId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionResourceId)
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
