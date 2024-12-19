param vaultName string
param location string
param environment string
@secure()
param tenant_id string
@secure()
param namePrefix string
@secure()
param test_client_id string
@export()
type Sku = {
  name: 'standard'
  family: 'A'
}
param sku Sku

// add identity to the keyvault
resource kvUserAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-kv-identity'
  location: location
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  properties: {
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    enabledForDeployment: true
    sku: sku
    tenantId: tenant_id
    accessPolicies: environment == 'test'
      ? [
          {
            applicationId: null
            tenantId: tenant_id
            objectId: test_client_id
            permissions: {
              keys: []
              secrets: [
                'Get'
                'List'
                'Set'
              ]
              certificates: []
            }
          }
        ]
      : [
          {
            applicationId: null
            tenantId: kvUserAssignedIdentity.properties.tenantId
            objectId: kvUserAssignedIdentity.properties.principalId
            permissions: {
              keys: []
              secrets: [
                'Get'
                'List'
                'Set'
              ]
              certificates: []
            }
          }
        ]
  }
}

var secrets = [
  {
    name: 'kv-url'
    value: keyVault.properties.vaultUri
  }
  {
    name: 'kv-tenant-id'
    value: keyVault.properties.accessPolicies[0].tenantId
  }
  {
    name: 'kv-client-secret'
    value: keyVault.properties.accessPolicies[0].permissions.keys[0].keyId
  }
  {
    name: 'kv-client-id'
    value: keyVault.properties.accessPolicies[0].objectId
  }
]

module keyvaultSecrets '../keyvault/upsertSecrets.bicep' = {
  name: 'secrets'
  params: {
    secrets: secrets
    sourceKeyvaultName: keyVault.name
  }
}

output name string = keyVault.name
