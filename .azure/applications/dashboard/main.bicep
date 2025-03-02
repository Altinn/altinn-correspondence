param location string
param containerImage string
param azureNamePrefix string
param keyVaultName string

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: keyVaultName
}

resource secret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' existing = {
  parent: keyVault
  name: 'correspondence-migration-connection-string'
}

// Register the secret with the container app environment
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${azureNamePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
    }
    secrets: [
      {
        name: 'correspondence-migration-connection-string'
        value: secret.properties.value
      }
    ]
  }
} 

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'dashboard'
  location: location
  properties: {
    environmentId: '${azureNamePrefix}-env'
    configuration: {
      ingress: {
        external: true
        targetPort: 2526
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'dashboard'
          image: containerImage
          env: [
            {
              name: 'DatabsaeOptions__ConnectionString'
              value: secret.properties.value
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

output dashboardUrl string = containerApp.outputs.url 
