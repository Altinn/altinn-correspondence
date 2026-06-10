targetScope = 'subscription'

param userAssignedIdentityPrincipalId string

// Required so Defender for Storage can assign Storage Blob Data Owner to the StorageDataScanner
// managed identity when enabling on-upload malware scanning. Defender for Storage Scanner Operator
// alone is not sufficient because its ABAC condition only permits a subset of role assignments.
var rbacAdministratorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'f58310d9-a9f6-439a-9e8d-f62e7b41a168')

resource rbacAdministratorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, userAssignedIdentityPrincipalId, rbacAdministratorRoleId)
  properties: {
    roleDefinitionId: rbacAdministratorRoleId
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
