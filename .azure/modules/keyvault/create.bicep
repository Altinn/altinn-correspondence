param vaultName string
param location string
@secure()
param tenant_id string

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
    accessPolicies: []
  }
}

output name string = keyVault.name
