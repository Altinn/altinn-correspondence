param storageAccountName string
param principalId string
param principalType string = 'ServicePrincipal'

// Storage File Data Privileged Contributor role
// Required for OAuth access to upload migration files
var storageFileDataPrivilegedContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69566ab7-960f-475b-8e7c-b3118f30c6bd')

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' existing = {
  name: storageAccountName
}

resource fileDataPrivilegedContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, storageAccount.id, principalId, 'storage-file-data-privileged-contributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageFileDataPrivilegedContributorRoleId
    principalId: principalId
    principalType: principalType
  }
}

