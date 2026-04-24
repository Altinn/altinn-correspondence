targetScope = 'subscription'

@minLength(3)
param imageTag string
@minLength(3)
param environment string
@minLength(3)
param location string
@secure()
@minLength(3)
param sourceKeyVaultName string
@secure()
param keyVaultUrl string
@secure()
param namePrefix string
@secure()
param storageAccountName string
@secure()
param apimIp string

var image = 'ghcr.io/altinn/altinn-correspondence:${imageTag}'
var containerAppName = '${namePrefix}-app'
var testRotationVault = {
  environment: 'test'
  resourceGroupName: 'altinn-corr-test-rg'
  keyVaultName: 'altinn-corr-test-kv'
}
var at22RotationVault = {
  environment: 'at22'
  resourceGroupName: 'altinn-corr-at22-rg'
  keyVaultName: 'altinn-corr-at22-kv'
}
var at23RotationVault = {
  environment: 'at23'
  resourceGroupName: 'altinn-corr-at23-rg'
  keyVaultName: 'altinn-corr-at23-kv'
}
var at24RotationVault = {
  environment: 'at24'
  resourceGroupName: 'altinn-corr-at24-rg'
  keyVaultName: 'altinn-corr-at24-kv'
}
var stagingRotationVault = {
  environment: 'staging'
  resourceGroupName: 'altinn-corr-staging-rg'
  keyVaultName: 'altinn-corr-staging-kv'
}
var yt01RotationVault = {
  environment: 'yt01'
  resourceGroupName: 'altinn-corr-yt01-rg'
  keyVaultName: 'altinn-corr-yt01-kv'
}
var additionalRotationVaults = environment == 'test'
  ? [at22RotationVault, at23RotationVault, at24RotationVault, stagingRotationVault, yt01RotationVault]
  : environment == 'at22'
    ? [testRotationVault, at23RotationVault, at24RotationVault, stagingRotationVault, yt01RotationVault]
    : environment == 'at23'
      ? [testRotationVault, at22RotationVault, at24RotationVault, stagingRotationVault, yt01RotationVault]
      : environment == 'at24'
        ? [testRotationVault, at22RotationVault, at23RotationVault, stagingRotationVault, yt01RotationVault]
        : environment == 'staging'
          ? [testRotationVault, at22RotationVault, at23RotationVault, at24RotationVault, yt01RotationVault]
          : environment == 'yt01'
            ? [testRotationVault, at22RotationVault, at23RotationVault, at24RotationVault, stagingRotationVault]
            : []

var resourceGroupName = '${namePrefix}-rg'
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' existing = {
  name: resourceGroupName
}

module appIdentity '../../modules/identity/create.bicep' = {
  name: 'appIdentity'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
  }
}

module addContributorAccess '../../modules/identity/addContributorAccess.bicep' = {
  name: 'appDeployToAzureAccess'
  params: {
    userAssignedIdentityPrincipalId: appIdentity.outputs.principalId
  }
}

module addStorageBlobDataContributor '../../modules/identity/addStorageBlobDataContributorRole.bicep' = {
  name: 'storageBlobDataContributorAccess'
  params: {
    userAssignedIdentityPrincipalId: appIdentity.outputs.principalId
  }
}

module keyvaultAddReaderRolesAppIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-app'
  scope: resourceGroup
  params: {
    keyvaultName: sourceKeyVaultName
    principals: [{ objectId: appIdentity.outputs.principalId, principalType: 'ServicePrincipal'}]
  }
}

module keyvaultAddSecretsOfficerRoleAppIdentity '../../modules/keyvault/addSecretsOfficerRole.bicep' = {
  name: 'kvsecretsofficer-${namePrefix}-app'
  scope: resourceGroup
  params: {
    keyvaultName: sourceKeyVaultName
    principalType: 'ServicePrincipal'
    principalObjectId: appIdentity.outputs.principalId
  }
}

resource additionalRotationVaultResourceGroups 'Microsoft.Resources/resourceGroups@2024-11-01' existing = [for vault in additionalRotationVaults: {
  name: vault.resourceGroupName
}]

module additionalRotationVaultReaderRoles '../../modules/keyvault/addReaderRoles.bicep' = [for (vault, i) in additionalRotationVaults: {
  name: 'kvreader-${vault.environment}-${namePrefix}-app'
  scope: additionalRotationVaultResourceGroups[i]
  params: {
    keyvaultName: vault.keyVaultName
    principals: [{ objectId: appIdentity.outputs.principalId, principalType: 'ServicePrincipal'}]
  }
}]

module additionalRotationVaultSecretsOfficerRoles '../../modules/keyvault/addSecretsOfficerRole.bicep' = [for (vault, i) in additionalRotationVaults: {
  name: 'kvsecretsofficer-${vault.environment}-${namePrefix}-app'
  scope: additionalRotationVaultResourceGroups[i]
  params: {
    keyvaultName: vault.keyVaultName
    principalType: 'ServicePrincipal'
    principalObjectId: appIdentity.outputs.principalId
  }
}]

module databaseAccess '../../modules/postgreSql/addAdminAccess.bicep' = {
  name: 'databaseAccess'
  scope: resourceGroup
  dependsOn: [
    keyvaultAddReaderRolesAppIdentity // Timing issue
    keyvaultAddSecretsOfficerRoleAppIdentity
  ]
  params: {
    principalType: 'ServicePrincipal'
    tenantId: appIdentity.outputs.tenantId
    principalId: appIdentity.outputs.principalId
    appName: appIdentity.outputs.name
    namePrefix: namePrefix
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: sourceKeyVaultName
  scope: resourceGroup
}

module fetchEventGridIpsScript '../../modules/containerApp/fetchEventGridIps.bicep' = {
  name: 'fetchAzureEventGridIpsScript'
  scope: resourceGroup
  dependsOn: [keyvaultAddReaderRolesAppIdentity, databaseAccess, addContributorAccess]
  params: {
    location: location
    principal_id: appIdentity.outputs.id
  }
}

module containerApp '../../modules/containerApp/main.bicep' = {
  name: containerAppName
  scope: resourceGroup
  dependsOn: [
    keyvaultAddReaderRolesAppIdentity
    keyvaultAddSecretsOfficerRoleAppIdentity
    additionalRotationVaultReaderRoles
    additionalRotationVaultSecretsOfficerRoles
    databaseAccess
  ]
  params: {
    eventGridIps: fetchEventGridIpsScript.outputs.eventGridIps!
    namePrefix: namePrefix
    image: image
    location: location
    environment: environment
    apimIp: apimIp
    principal_id: appIdentity.outputs.id
    userIdentityClientId: appIdentity.outputs.clientId
    keyVaultUrl: keyVaultUrl
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
  }
}

module virusScan '../../modules/virusScan/create.bicep' = {
  scope: resourceGroup
  name: 'virusScan'
  params: {
    containerAppIngress: containerApp.outputs.containerAppIngress
    location: location
    namePrefix: namePrefix
    storageAccountName: storageAccountName
  }
}

output name string = containerApp.outputs.name
output revisionName string = containerApp.outputs.revisionName
