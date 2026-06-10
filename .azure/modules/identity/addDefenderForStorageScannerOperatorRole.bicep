targetScope = 'subscription'

param userAssignedIdentityPrincipalId string

var defenderForStorageScannerOperatorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0f641de8-0b88-4198-bdef-bd8b45ceba96')

resource defenderForStorageScannerOperatorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentityPrincipalId, defenderForStorageScannerOperatorRoleId)
  properties: {
    roleDefinitionId: defenderForStorageScannerOperatorRoleId
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
