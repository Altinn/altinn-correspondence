param namePrefix string
param location string
param appVersion string

@secure()
param keyVaultUrl string
@secure()
param keyVaultName string
@minLength(3)
param environment string

var containerAppJobName = '${namePrefix}-migration'
var containerAppEnvName = '${namePrefix}-env'
var migrationConnectionStringName = 'correspondence-migration-connection-string'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-migration-identity'
  location: location
}

module addKeyvaultRead '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-migration'
  params: {
    keyvaultName: keyVaultName
    tenantId: userAssignedIdentity.properties.tenantId
    principalIds: [userAssignedIdentity.properties.principalId]
  }
}
module databaseAccess '../../modules/postgreSql/AddAdministrationAccess.bicep' = {
  name: 'databaseAccess'
  dependsOn: [
    addKeyvaultRead // Timing issue
  ]
  params: {
    tenantId: userAssignedIdentity.properties.tenantId
    principalId: userAssignedIdentity.properties.principalId
    appName: userAssignedIdentity.name
    namePrefix: namePrefix
  }
}
var secrets = [
  {
    name: migrationConnectionStringName
    keyVaultUrl: '${keyVaultUrl}/secrets/${migrationConnectionStringName}'
    identity: userAssignedIdentity.id
  }
]

var containerAppEnvVars = [
  {
    name: 'APP_VERSION'
    value: appVersion
  }
  {
    name: 'DatabaseOptions__ConnectionString'
    secretRef: migrationConnectionStringName
  }
  {
    name: 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT'
    value: '1'
  }
  { name: 'AzureResourceManagerOptions__SubscriptionId', value: subscription().subscriptionId }
  { name: 'AzureResourceManagerOptions__Location', value: 'norwayeast' }
  { name: 'AzureResourceManagerOptions__Environment', value: environment }
  { name: 'AzureResourceManagerOptions__ApplicationResourceGroupName', value: '${namePrefix}-rg' }
  { name: 'AZURE_CLIENT_ID', value: userAssignedIdentity.properties.clientId }
]

var volumes = [
  {
    name: 'migrations'
    storageName: 'migrations'
    storageType: 'AzureFile'
    mountOptions: 'cache=none'
  }
]

var volumeMounts = [
  {
    volumeName: 'migrations'
    mountPath: '/migrations'
    subPath: ''
  }
]

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppEnvName
}

module containerAppJob '../../modules/migrationJob/main.bicep' = {
  name: containerAppJobName
  dependsOn: [
    addKeyvaultRead
  ]
  params: {
    name: containerAppJobName
    location: location
    containerAppEnvId: containerAppEnv.id
    environmentVariables: containerAppEnvVars
    secrets: secrets
    command: ['/bin/bash', '-c', './migrations/bundle.exe --connection "$(DatabaseOptions__ConnectionString)";']
    image: 'ubuntu:latest'
    volumes: volumes
    volumeMounts: volumeMounts
    principalId: userAssignedIdentity.id
  }
}

output name string = containerAppJob.name
