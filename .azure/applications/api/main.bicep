targetScope = 'resourceGroup'

@minLength(3)
param imageTag string
@minLength(3)
param environment string
@minLength(3)
param location string
@minLength(3)
param platform_base_url string
@secure()
@minLength(3)
param sourceKeyVaultName string
@secure()
param keyVaultUrl string

@secure()
param client_id string

@secure()
param tenant_id string
@secure()
param namePrefix string

var baseImageUrl = 'ghcr.io/altinn/altinn-correspondence'
var containerAppName = '${namePrefix}-app'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-app-identity'
  location: location
}

module keyVaultReaderAccessPolicyUserIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-app'
  params: {
    keyvaultName: sourceKeyVaultName
    tenantId: userAssignedIdentity.properties.tenantId
    principalIds: [userAssignedIdentity.properties.principalId]
  }
}

module databaseAccess '../../modules/postgreSql/AddAdministrationAccess.bicep' = {
  name: 'databaseAccess'
  dependsOn: [
    keyVaultReaderAccessPolicyUserIdentity // Timing issue
  ]
  params: {
    tenantId: userAssignedIdentity.properties.tenantId
    principalId: userAssignedIdentity.properties.principalId
    appName: userAssignedIdentity.name
    namePrefix: namePrefix
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: sourceKeyVaultName
}

module containerApp '../../modules/containerApp/main.bicep' = {
  name: containerAppName
  dependsOn: [keyVaultReaderAccessPolicyUserIdentity, databaseAccess]
  params: {
    namePrefix: namePrefix
    image: '${baseImageUrl}:${imageTag}'
    location: location
    environment: environment
    client_id: client_id
    tenant_id: tenant_id
    subscription_id: subscription().subscriptionId
    principal_id: userAssignedIdentity.id
    platform_base_url: platform_base_url
    keyVaultUrl: keyVaultUrl
    userIdentityClientId: userAssignedIdentity.properties.clientId
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
  }
}

output name string = containerApp.outputs.name
output revisionName string = containerApp.outputs.revisionName
