param location string
@secure()
param namePrefix string
param image string
param environment string
param platform_base_url string
param maskinporten_environment string
param correspondenceBaseUrl string
param idportenIssuer string
param dialogportenIssuer string
param maskinporten_token_exchange_environment string

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
    type: 'Liveness'
    httpGet: {
      path: '/health'
      port: 2525
    }
    periodSeconds: 10
    failureThreshold: 3
    initialDelaySeconds: 15
  }
  {
    type: 'Readiness'
    httpGet: {
      path: '/health'
      port: 2525
    }
    periodSeconds: 10
    failureThreshold: 3
    initialDelaySeconds: 15
  }
]

var containerAppEnvVars = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: environment }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'application-insights-connection-string' }
  { name: 'DatabaseOptions__ConnectionString', secretRef: 'correspondence-ado-connection-string' }
  { name: 'AttachmentStorageOptions__ConnectionString', secretRef: 'storage-connection-string' }
  { name: 'GeneralSettings__RedisConnectionString', secretRef: 'redis-connection-string' }
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
  { name: 'AltinnOptions__PlatformSubscriptionKey', secretRef: 'platform-subscription-key' }
  { name: 'AltinnOptions__AccessManagementSubscriptionKey', secretRef: 'access-management-subscription-key' }
  { name: 'MaskinportenSettings__Environment', value: maskinporten_environment }
  { name: 'MaskinportenSettings__ClientId', secretRef: 'maskinporten-client-id' }
  {
    name: 'MaskinportenSettings__Scope'
    value: 'altinn:events.publish altinn:events.publish.admin altinn:register/partylookup.admin altinn:authorization/authorize.admin altinn:serviceowner/notifications.create altinn:serviceowner/notifications.read digdir:dialogporten.serviceprovider digdir:dialogporten.serviceprovider.admin altinn:accessmanagement/authorizedparties.admin altinn:accessmanagement/authorizedparties.resourceowner'
  }
  {
    name: 'MaskinportenSettings__ExhangeToAltinnToken'
    value: 'true'
  }
  { name: 'MaskinportenSettings__TokenExchangeEnvironment', value: maskinporten_token_exchange_environment }
  { name: 'MaskinportenSettings__EncodedJwk', secretRef: 'maskinporten-jwk' }
  { name: 'GeneralSettings__CorrespondenceBaseUrl', value: correspondenceBaseUrl }
  { name: 'GeneralSettings__SlackUrl', secretRef: 'slack-url' }
  { name: 'DialogportenSettings__Issuer', value: dialogportenIssuer }
  { name: 'IdportenSettings__Issuer', value: idportenIssuer }
  { name: 'IdportenSettings__ClientId', secretRef: 'idporten-client-id' }
  { name: 'IdportenSettings__ClientSecret', secretRef: 'idporten-client-secret' }
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
          keyVaultUrl: '${keyVaultUrl}/secrets/platform-subscription-key'
          name: 'platform-subscription-key'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/maskinporten-client-id'
          name: 'maskinporten-client-id'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/maskinporten-jwk'
          name: 'maskinporten-jwk'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/access-management-subscription-key'
          name: 'access-management-subscription-key'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/slack-url'
          name: 'slack-url'
        }
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
          keyVaultUrl: '${keyVaultUrl}/secrets/storage-connection-string'
          name: 'storage-connection-string'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/idporten-client-id'
          name: 'idporten-client-id'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/idporten-client-secret'
          name: 'idporten-client-secret'
        }
        {
          identity: principal_id
          keyVaultUrl: '${keyVaultUrl}/secrets/redis-connection-string'
          name: 'redis-connection-string'
        }
      ]
    }

    environmentId: containerAppEnvId
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
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
output containerAppIngress string = containerApp.properties.configuration.ingress.fqdn
