param keyvaultName string
param principals array
param tenantId string = subscription().tenantId

var secretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var keyVaultReaderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '21090545-7ca7-4776-b22c-e363652d74d2')

var readerAccessPoliciesArray = [
  for principal in principals: {
    objectId: principal.objectId
    tenantId: tenantId
    permissions: {
      certificates: ['get', 'list']
      keys: ['get', 'list']
      secrets: ['get', 'list']
    }
  }
]

resource kv 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyvaultName
  resource readerAccessPolicies 'accessPolicies' = {
    name: 'add'
    properties: {
      accessPolicies: readerAccessPoliciesArray
    }
  }
}

resource secretsUsers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for p in principals: {
  name: guid(subscription().id, kv.id, p.objectId, 'kv-secrets-user')
  scope: kv
  properties: {
    roleDefinitionId: secretsUserRoleId
    principalId: p.objectId
    principalType: p.principalType
  }
}]

resource kvReaders 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for p in principals: {
  name: guid(subscription().id,kv.id, p.objectId, 'kv-reader')
  scope: kv
  properties: {
    roleDefinitionId: keyVaultReaderRoleId
    principalId: p.objectId
    principalType: p.principalType
  }
}]
