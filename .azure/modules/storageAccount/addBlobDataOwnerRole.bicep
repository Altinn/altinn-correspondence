targetScope = 'subscription'
param principalId string
param principalType string = 'Group'

// Storage Blob Data Owner role (full data plane access)
var storageBlobDataOwnerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')

resource blobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, principalId, 'storage-blob-data-owner')
  properties: {
    roleDefinitionId: storageBlobDataOwnerRoleId
    principalId: principalId
    principalType: principalType
  }
}
