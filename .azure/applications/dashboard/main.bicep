param location string
param containerImage string
param azureNamePrefix string
param keyVaultName string

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: keyVaultName
}

resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  name: '${keyVaultName}/add'
  properties: {
    accessPolicies: [
      {
        objectId: containerApp.identity.principalId
        tenantId: subscription().tenantId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${azureNamePrefix}-dashboard'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: resourceId('Microsoft.App/managedEnvironments', '${azureNamePrefix}-env')
    configuration: {
      ingress: {
        external: true
        targetPort: 2526
        allowInsecure: false
      }
      secrets: [
        {
          name: 'correspondence-migration-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/correspondence-migration-connection-string'
          identity: 'System'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'dashboard'
          image: containerImage
          env: [
            {
              name: 'DatabsaeOptions__ConnectionString'
              secretRef: 'correspondence-migration-connection-string'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}
