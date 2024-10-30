targetScope = 'subscription'
@minLength(3)
param location string
@secure()
param correspondencePgAdminPassword string
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

import { Sku as KeyVaultSku } from '../modules/keyvault/create.bicep'
param keyVaultSku KeyVaultSku

import { Sku as PostgresSku } from '../modules/postgreSql/create.bicep'
param postgresSku PostgresSku

var resourceGroupName = '${namePrefix}-rg'

// Create resource groups
resource resourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-rg'
  location: location
}

module environmentKeyVault '../modules/keyvault/create.bicep' = {
  scope: resourceGroup
  name: 'keyVault'
  params: {
    vaultName: sourceKeyVaultName
    location: location
    sku: keyVaultSku
    tenant_id: tenantId
    environment: environment
    test_client_id: test_client_id
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

var srcKeyVault = {
  name: sourceKeyVaultName
  subscriptionId: subscription().subscriptionId
  resourceGroupName: resourceGroupName
}

var correspondenceAdminPasswordSecretName = 'correspondence-admin-password'
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
    srcKeyVault: srcKeyVault
    srcSecretName: correspondenceAdminPasswordSecretName
    administratorLoginPassword: correspondencePgAdminPassword
    sku: postgresSku
    tenantId: tenantId
  }
}

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
  dependsOn: [storageAccount]
  params: {
    keyVaultName: sourceKeyVaultName
    location: location
    namePrefix: namePrefix
    storageAccountName: storageAccountName
  }
}
output resourceGroupName string = resourceGroup.name
output environmentKeyVaultName string = environmentKeyVault.outputs.name
