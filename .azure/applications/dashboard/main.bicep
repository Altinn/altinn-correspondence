param location string
param containerImage string
@secure()
param azureNamePrefix string
@secure()
param keyVaultName string
@secure()
param appRegistrationId string
@secure()
param appRegistrationClientSecret string
@secure()
param tenantId string
@secure()
param allowedGroupId string
@secure()
param storageAccountName string


module appIdentity '../../modules/identity/create.bicep' = {
  name: 'dashboardIdentity'
  params: {
    namePrefix: '${azureNamePrefix}-dashboard'
    location: location
  }
}

module keyvaultAccess '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'keyvaultAccess'
  params: {
    keyvaultName: keyVaultName
    principalIds: [
      appIdentity.outputs.principalId
    ]
    tenantId: tenantId
  }
}

module dashboard '../../modules/containerApp/dashboard.bicep' = {
  name: 'dashboard'
  dependsOn: [
    keyvaultAccess
  ]
  params: {
    containerImage: containerImage
    location: location
    azureNamePrefix: azureNamePrefix
    keyVaultName: keyVaultName
    appRegistrationId: appRegistrationId
    appRegistrationClientSecret: appRegistrationClientSecret
    tenantId: tenantId
    allowedGroupId: allowedGroupId
    storageAccountName: storageAccountName
    userAssignedIdentityId: appIdentity.outputs.id
    userAssignedIdentityClientId: appIdentity.outputs.clientId
    userAssignedIdentityPrincipalId: appIdentity.outputs.principalId
  }
}
