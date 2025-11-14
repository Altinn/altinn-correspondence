targetScope = 'subscription'

@minLength(3)
param imageTag string
@minLength(3)
param environment string
@minLength(3)
param location string
@minLength(3)
param platform_base_url string
@secure()
param override_authorization_url string
@secure()
param override_authorization_thumbprint string
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
param sblBridgeBaseUrl string
@minLength(3)
param maskinporten_environment string
param correspondenceBaseUrl string
param contactReservationRegistryBaseUrl string
param brregBaseUrl string
param idportenIssuer string
param dialogportenIssuer string
param maskinporten_token_exchange_environment string
@secure()
@minLength(3)
param apimIp string
param migrationWorkerCountPerReplica string
param bruksmonsterTestsResourceId string
param arbeidsflateOriginsCommaSeparated string

var image = 'ghcr.io/altinn/altinn-correspondence:${imageTag}'
var containerAppName = '${namePrefix}-app'

var resourceGroupName = '${namePrefix}-rg'
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
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
    subscription_id: subscription().subscriptionId
    principal_id: appIdentity.outputs.id
    platform_base_url: platform_base_url
    override_authorization_url: override_authorization_url
    override_authorization_thumbprint: override_authorization_thumbprint
    keyVaultUrl: keyVaultUrl
    userIdentityClientId: appIdentity.outputs.clientId
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
    maskinporten_environment: maskinporten_environment
    correspondenceBaseUrl: correspondenceBaseUrl
    contactReservationRegistryBaseUrl: contactReservationRegistryBaseUrl
    brregBaseUrl: brregBaseUrl
    idportenIssuer: idportenIssuer
    dialogportenIssuer: dialogportenIssuer
    sblBridgeBaseUrl: sblBridgeBaseUrl
    maskinporten_token_exchange_environment: maskinporten_token_exchange_environment
    migrationWorkerCountPerReplica: migrationWorkerCountPerReplica
    bruksmonsterTestsResourceId: bruksmonsterTestsResourceId
    arbeidsflateOriginsCommaSeparated: arbeidsflateOriginsCommaSeparated
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
