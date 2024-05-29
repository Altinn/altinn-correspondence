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
param migrationsStorageAccountName string

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
    test_client_id: test_client_id
    environment: environment
  }
}

module storageAccount '../modules/storageAccount/create.bicep' = {
  scope: resourceGroup
  name: migrationsStorageAccountName
  params: {
    storageAccountName: migrationsStorageAccountName
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
    storageAccountName: migrationsStorageAccountName
  }
}
output resourceGroupName string = resourceGroup.name
output environmentKeyVaultName string = environmentKeyVault.outputs.name
