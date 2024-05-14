param namePrefix string
param location string
param appVersion string

@secure()
param keyVaultUrl string

@secure()
param keyVaultName string

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
    name: 'CONNECTION_STRING'
    secretRef: migrationConnectionStringName
  }
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

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-11-02-preview' existing = {
  name: containerAppEnvName
}

module containerAppJob '../../modules/containerAppJob/main.bicep' = {
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
    command: ['/bin/bash', '-c', './bundle.exe --connection {$CONNECTION_STRING};']
    image: 'ubuntu:latest'
    volumes: volumes
    volumeMounts: volumeMounts
    principalId: userAssignedIdentity.id
  }
}

output name string = containerAppJob.name
