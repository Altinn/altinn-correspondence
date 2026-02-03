// Infrastructure parameters
param location string
@secure()
param namePrefix string
param image string
param environment string
@secure()
param principal_id string
@secure()
param keyVaultUrl string
@secure()
param containerAppEnvId string

// Application configuration parameters
param eventGridIps array
@secure()
param apimIp string
@secure()
param userIdentityClientId string

type ContainerAppScale = {
    minReplicas: int
    maxReplicas: int
}

// Required Key Vault secret environment variables
var predefinedKeyvaultSecretEnvVars = [
  { name: 'AltinnOptions__PlatformSubscriptionKey', secretName: 'platform-subscription-key' }
  { name: 'AltinnOptions__AccessManagementSubscriptionKey', secretName: 'access-management-subscription-key' }
  { name: 'MaskinportenSettings__ClientId', secretName: 'maskinporten-client-id' }
  { name: 'MaskinportenSettings__EncodedJwk', secretName: 'maskinporten-jwk' }
  { name: 'GeneralSettings__SlackUrl', secretName: 'slack-url' }
  { name: 'IdportenSettings__ClientId', secretName: 'idporten-client-id' }
  { name: 'IdportenSettings__ClientSecret', secretName: 'idporten-client-secret' }
  { name: 'StatisticsApiKey', secretName: 'statistics-api-key' }
]

var setByPipelineSecretEnvVars = [
  { name: 'DatabaseOptions__ConnectionString', secretName: 'correspondence-ado-connection-string' }
  { name: 'AttachmentStorageOptions__ConnectionString', secretName: 'storage-connection-string' }
  { name: 'GeneralSettings__RedisConnectionString', secretName: 'redis-connection-string' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretName: 'application-insights-connection-string' }
  { name: 'GeneralSettings__ApplicationInsightsConnectionString', secretName: 'application-insights-connection-string' }
]

// In production we override authorization url to circumvent APIM to relieve load
var optionalOverrideAuthSecrets = environment == 'production' ? [
  { name: 'AltinnOptions__OverrideAuthorizationUrl', secretName: 'override-authorization-url' }
  { name: 'AltinnOptions__OverrideAuthorizationThumbprint', secretName: 'override-authorization-thumbprint' }
] : []

// Combine required and optional secrets
var alwaysSetEnvVars = concat(predefinedKeyvaultSecretEnvVars, setByPipelineSecretEnvVars)
var secretEnvVars = concat(alwaysSetEnvVars, optionalOverrideAuthSecrets)

// Extract secrets configuration from env var configs
var secrets = [for config in secretEnvVars: {
  identity: principal_id
  keyVaultUrl: '${keyVaultUrl}/secrets/${config.secretName}'
  name: config.secretName
}]

// Build environment variables array from configs
var containerAppEnvVarsFromConfig = [for config in secretEnvVars: {
  name: config.name
  secretRef: config.secretName
}]

// Additional computed environment variables (that need expressions)
var containerAppEnvVarsComputed = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: environment }
  { name: 'OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION', value: 'true' }
  { name: 'AZURE_CLIENT_ID', value: userIdentityClientId }
  { name: 'AzureResourceManagerOptions__SubscriptionId', value: subscription().subscriptionId }
  { name: 'AzureResourceManagerOptions__ApimIP', value: apimIp }
  ]

// Combine all environment variables
var containerAppEnvVars = concat(
  containerAppEnvVarsFromConfig,
  containerAppEnvVarsComputed
)

// Scaling
param prodLikeEnvironment bool = environment == 'production' || environment == 'staging' || environment == 'yt01'
param containerAppResources object = prodLikeEnvironment ? {
  cpu: 2
  memory: '4.0Gi'
} : {
  // Using json() as a workaround for Bicep float type limitations in Container Apps
  cpu: json('0.5')
  memory: '1.0Gi'
}
param containerAppScale ContainerAppScale = prodLikeEnvironment ? {
  minReplicas: 5
  maxReplicas: 10
} : {
  minReplicas: 1
  maxReplicas: 1
}

// Probes
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


var EventGridIpRestrictions = map(eventGridIps, (ipRange, index) => {
  name: 'AzureEventGrid'
  action: 'Allow'
  ipAddressRange: ipRange!
})

var apimIpRestrictions = empty(apimIp)
  ? []
  : [
      {
        name: 'apim'
        action: 'Allow'
        ipAddressRange: apimIp!
      }
    ]
var ipSecurityRestrictions = concat(apimIpRestrictions, EventGridIpRestrictions)

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
        ipSecurityRestrictions: ipSecurityRestrictions
      }
      secrets: secrets
    }

    environmentId: containerAppEnvId
    template: {
      scale: containerAppScale
      containers: [
        {
          name: 'app'
          image: image
          env: containerAppEnvVars
          probes: probes
          resources: containerAppResources
        }
      ]
    }
  }
}

output name string = containerApp.name
output revisionName string = containerApp.properties.latestRevisionName
output app object = containerApp
output containerAppIngress string = containerApp.properties.configuration.ingress.fqdn
