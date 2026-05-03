targetScope = 'subscription'

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
@description('GitHub registration URL, e.g. https://github.com/Altinn/altinn-correspondence')
param githubUrl string
@description('Container image for the self-hosted runner.')
param runnerImage string = 'ghcr.io/altinn/altinn-correspondence-github-runner:latest'

var resourceGroupName = '${namePrefix}-rg'
var appName = '${namePrefix}-github-runner'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' existing = {
  name: resourceGroupName
}

resource keyvault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: sourceKeyVaultName
  scope: resourceGroup
}

module runnerIdentity '../../modules/identity/create.bicep' = {
  name: 'githubRunnerIdentity'
  scope: resourceGroup
  params: {
    namePrefix: '${namePrefix}-github-runner'
    location: location
  }
}

module keyvaultAddReaderRolesRunnerIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-github-runner'
  scope: resourceGroup
  params: {
    keyvaultName: sourceKeyVaultName
    principals: [
      { objectId: runnerIdentity.outputs.principalId, principalType: 'ServicePrincipal' }
    ]
  }
}

module githubRunnerContainerApp '../../modules/githubRunnerContainerApp/main.bicep' = {
  name: appName
  scope: resourceGroup
  dependsOn: [
    keyvaultAddReaderRolesRunnerIdentity
  ]
  params: {
    namePrefix: namePrefix
    location: location
    userAssignedIdentityResourceId: runnerIdentity.outputs.id
    keyVaultUrl: keyVaultUrl
    containerAppEnvId: keyvault.getSecret('container-app-env-id')
    runnerImage: runnerImage
    githubUrl: githubUrl
  }
}

output name string = githubRunnerContainerApp.outputs.name
output revisionName string = githubRunnerContainerApp.outputs.revisionName
