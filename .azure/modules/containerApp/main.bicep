param location string
@secure()
param namePrefix string
param image string
param environment string
param platform_base_url string

@secure()
param subscription_id string
@secure()
param principal_id string
@secure()
param keyVaultUrl string
@secure()
param userIdentityClientId string

@secure()
param containerAppEnvId string

var probes = [
  {
    httpGet: {
      port: 2525
      path: '/health'
    }
    type: 'Startup'
  }
]

var containerAppEnvVars = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: environment }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'application-insights-connection-string' }
  { name: 'DatabaseOptions__ConnectionString', secretRef: 'correspondence-ado-connection-string' }
  { name: 'AttachmentStorageOptions__ConnectionString', secretRef: 'storage-account-key'}
  { name: 'AzureResourceManagerOptions__SubscriptionId', value: subscription_id }
  { name: 'AzureResourceManagerOptions__Location', value: 'norwayeast' }
  { name: 'AzureResourceManagerOptions__Environment', value: environment }
  { name: 'AzureResourceManagerOptions__ApplicationResourceGroupName', value: '${namePrefix}-rg' }
  { name: 'AZURE_CLIENT_ID', value: userIdentityClientId }
  {
    name: 'AltinnOptions__OpenIdWellKnown'
    value: '${platform_base_url}/authentication/api/v1/openid/.well-known/openid-configuration'
  }
  { name: 'AltinnOptions__PlatformGatewayUrl', value: platform_base_url }
]
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${namePrefix}-app'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principal_id}': {}
    }
  }
  properties: {
    configuration: {
      ingress: {
        targetPort: 2525
        external: true
        transport: 'Auto'
      }
      secrets: [
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/application-insights-connection-string'
          name: 'application-insights-connection-string'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/correspondence-ado-connection-string'
          name: 'correspondence-ado-connection-string'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/storage-account-key'
          name: 'storage-account-key'
        }
      ]
    }

    environmentId: containerAppEnvId
    template: {
      containers: [
        {
          name: 'app'
          image: image
          env: containerAppEnvVars
          probes: probes
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
        }
      ]
    }
  }
}

output name string = containerApp.name
output revisionName string = containerApp.properties.latestRevisionName
output app object = containerApp
