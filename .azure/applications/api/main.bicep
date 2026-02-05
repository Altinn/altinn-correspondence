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

module databaseAccess '../../modules/postgreSql/addAdminAccess.bicep' = {
  name: 'databaseAccess'
  scope: resourceGroup
  dependsOn: [
    keyvaultAddReaderRolesAppIdentity // Timing issue
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
  dependsOn: [keyvaultAddReaderRolesAppIdentity, databaseAccess]
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
