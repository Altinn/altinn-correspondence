targetScope = 'subscription'
@minLength(3)
param location string
@secure()
param sourceKeyVaultName string
@secure()
param tenantId string
@secure()
param test_client_id string
param environment string
@secure()
param namePrefix string
@secure()
param maskinportenJwk string
@secure()
param maskinportenClientId string
@secure()
param platformSubscriptionKey string
@secure()
param accessManagementSubscriptionKey string
@secure()
param slackUrl string
@secure()
param idportenClientId string
@secure()
param idportenClientSecret string

@secure()
param storageAccountName string
param maskinporten_token_exchange_environment string
@secure()
param statisticsApiKey string
@secure()
param grafanaMonitoringPrincipalId string

@secure()
param maintenanceAdGroupId string
@secure()
param maintenanceAdGroupName string

var prodLikeEnvironment = environment == 'production' || environment == 'staging' || maskinporten_token_exchange_environment == 'yt01'
var resourceGroupName = '${namePrefix}-rg'
var standardTags = {
  finops_environment: environment
  finops_product: 'melding'
  finops_serviceownercode: 'digdir'
  finops_serviceownerorgnr: '991825827'
  repository: 'https://github.com/Altinn/altinn-correspondence'
  env: environment
  product: 'melding'
  org: 'digdir'
}

// Create resource groups
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: standardTags
}

module environmentKeyVault '../modules/keyvault/create.bicep' = {
  scope: resourceGroup
  name: 'keyVault'
  params: {
    vaultName: sourceKeyVaultName
    location: location
    tenant_id: tenantId
  }
}

module grantTestClientSecretsOfficerRole '../modules/keyvault/addSecretsOfficerRole.bicep' = if (environment == 'test') {
  scope: resourceGroup
  name: 'kv-secrets-officer-test-client'
  dependsOn: [ environmentKeyVault ]
  params: {
    keyvaultName: sourceKeyVaultName
    principalObjectId: test_client_id
    principalType: 'Group'
  }
}

var secrets = [
  {
    name: 'maskinporten-client-id'
    value: maskinportenClientId
  }
  {
    name: 'maskinporten-jwk'
    value: maskinportenJwk
  }
  {
    name: 'platform-subscription-key'
    value: platformSubscriptionKey
  }
  {
    name: 'access-management-subscription-key'
    value: accessManagementSubscriptionKey
  }
  {
    name: 'slack-url'
    value: slackUrl
  }
  {
    name: 'idporten-client-id'
    value: idportenClientId
  }
  {
    name: 'idporten-client-secret'
    value: idportenClientSecret
  }
  {
    name: 'statistics-api-key'
    value: statisticsApiKey
  }
]

module keyvaultSecrets '../modules/keyvault/upsertSecrets.bicep' = {
  scope: resourceGroup
  name: 'secrets'
  params: {
    secrets: secrets
    sourceKeyvaultName: environmentKeyVault.outputs.name
  }
}

// #####################################################
// Create resources with dependencies to other resources
// #####################################################

module storageAccount '../modules/storageAccount/create.bicep' = {
  scope: resourceGroup
  name: storageAccountName
  params: {
    storageAccountName: storageAccountName
    location: location
    fileshare: 'migrations'
  }
}

module containerAppEnv '../modules/containerAppEnvironment/main.bicep' = {
  scope: resourceGroup
  name: 'container-app-environment'
  params: {
    keyVaultName: sourceKeyVaultName
    location: location
    namePrefix: namePrefix
    storageAccountName: storageAccountName
  }
}

module postgresql '../modules/postgreSql/create.bicep' = {
  scope: resourceGroup
  name: 'postgresql'
  dependsOn: [
    environmentKeyVault
  ]
  params: {
    namePrefix: namePrefix
    location: location
    environmentKeyVaultName: sourceKeyVaultName
    tenantId: tenantId
    prodLikeEnvironment: prodLikeEnvironment
    logAnalyticsWorkspaceId: containerAppEnv.outputs.logAnalyticsWorkspaceId
    auditLogAnalyticsWorkspaceId: containerAppEnv.outputs.auditLogAnalyticsWorkspaceId
    environment: environment
  }
}

module maintenanceDbAccess '../modules/postgreSql/addAdminAccess.bicep' = {
  name: 'databaseAccess'
  scope: resourceGroup
  dependsOn: [
    postgresql
  ]
  params: {
    principalType: 'Group'
    tenantId: tenantId
    principalId: maintenanceAdGroupId
    appName: maintenanceAdGroupName
    namePrefix: namePrefix
  }
}

module reddis '../modules/redis/main.bicep' = {
  scope: resourceGroup
  name: 'redis'
  params: {
    location: location
    namePrefix: namePrefix
    keyVaultName: sourceKeyVaultName
    prodLikeEnvironment: prodLikeEnvironment
    environment: environment
  }
}

module grafanaMonitoringReaderRole '../modules/subscription/addMonitoringReaderRole.bicep' = {
  name: 'grafana-monitoring-reader'
  params: {
    grafanaPrincipalId: grafanaMonitoringPrincipalId
  }
}

module correspondenceTagsPolicy '../modules/policy/correspondenceTagsPolicy.bicep' = {
  name: 'correspondence-standard-tags-definition'
  params: {
    environment: environment
  }
}

module correspondenceTagsAssignment '../modules/policy/assignCorrespondenceTags.bicep' = {
  name: 'correspondence-standard-tags-assignment'
  scope: resourceGroup
  params: {
    policyDefinitionId: correspondenceTagsPolicy.outputs.policyDefinitionId
    userAssignedIdentityName: '${namePrefix}-correspondence-tags-mi'
  }
}

output resourceGroupName string = resourceGroup.name
output environmentKeyVaultName string = environmentKeyVault.outputs.name
