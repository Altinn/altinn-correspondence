param vaultName string
param location string
param environment string
@secure()
param tenant_id string
@secure()
param test_client_id string

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  properties: {
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    enabledForDeployment: true
    sku: {
      name: 'standard'
      family: 'A'
    }
    tenantId: tenant_id
    enableRbacAuthorization: true
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
      : []
  }
}

output name string = keyVault.name
