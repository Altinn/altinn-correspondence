param namePrefix string
param location string
param appVersion string

@secure()
param keyVaultUrl string
@secure()
param keyVaultName string
@minLength(3)
param environment string
@secure()
param apimIp string
@secure()
param storageAccountName string

var containerAppJobName = '${namePrefix}-migration'
var containerAppEnvName = '${namePrefix}-env'
var migrationConnectionStringName = 'correspondence-migration-connection-string'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-migration-identity'
  location: location
}

module keyvaultAddReaderRolesMigrationIdentity '../../modules/keyvault/addReaderRoles.bicep' = {
  name: 'kvreader-${namePrefix}-migration'
  params: {
    keyvaultName: keyVaultName
    principals: [
      { objectId: userAssignedIdentity.properties.principalId, principalType: 'ServicePrincipal' }
    ]
  }
}
module databaseAccess '../../modules/postgreSql/addAdminAccess.bicep' = {
  name: 'databaseAccess'
  dependsOn: [
    keyvaultAddReaderRolesMigrationIdentity // Timing issue
  ]
  params: {
    tenantId: userAssignedIdentity.properties.tenantId
    principalId: userAssignedIdentity.properties.principalId
    appName: userAssignedIdentity.name
    namePrefix: namePrefix
    principalType: 'ServicePrincipal'
  }
}

module grantMigrationIdentityStorageFileAccess '../../modules/storageAccount/addFileDataPrivilegedContributorRole.bicep' = {
  name: 'storage-file-privileged-contributor-migration'
  params: {
    storageAccountName: storageAccountName
    principalId: userAssignedIdentity.properties.principalId
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
  {
    name: 'DatabaseOptions__CommandTimeout'
    value: '3600'
  }
  { name: 'AzureResourceManagerOptions__SubscriptionId', value: subscription().subscriptionId }
  { name: 'AzureResourceManagerOptions__Location', value: 'norwayeast' }
  { name: 'AzureResourceManagerOptions__Environment', value: environment }
  { name: 'AzureResourceManagerOptions__ApplicationResourceGroupName', value: '${namePrefix}-rg' }
  { name: 'AzureResourceManagerOptions__ContainerAppName', value: '${namePrefix}-app' }
  { name: 'AzureResourceManagerOptions__ApimIP', value: apimIp }
  { name: 'AZURE_CLIENT_ID', value: userAssignedIdentity.properties.clientId }
  { name: 'STORAGE_ACCOUNT_NAME', value: storageAccountName }
  { name: 'FILE_SHARE_NAME', value: 'migrations' }
]

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-11-02-preview' existing = {
  name: containerAppEnvName
}

module containerAppJob '../../modules/migrationJob/main.bicep' = {
  name: containerAppJobName
  dependsOn: [
    keyvaultAddReaderRolesMigrationIdentity
  ]
  params: {
    name: containerAppJobName
    location: location
    containerAppEnvId: containerAppEnv.id
    environmentVariables: containerAppEnvVars
    secrets: secrets
    command: ['/bin/bash', '-c', 'apt-get update && apt-get install -y wget unzip && wget -O /tmp/azcopy.tar.gz https://aka.ms/downloadazcopy-v10-linux && tar -xzf /tmp/azcopy.tar.gz -C /tmp --strip-components=1 && export PATH=$PATH:/tmp && export AZCOPY_AUTO_LOGIN_TYPE=MSI && export AZCOPY_MSI_CLIENT_ID=$AZURE_CLIENT_ID && azcopy copy "https://$STORAGE_ACCOUNT_NAME.file.core.windows.net/$FILE_SHARE_NAME/bundle.exe" "/tmp/bundle.exe" --backup && azcopy copy "https://$STORAGE_ACCOUNT_NAME.file.core.windows.net/$FILE_SHARE_NAME/appsettings.json" "/tmp/appsettings.json" --backup && chmod +x /tmp/bundle.exe && cd /tmp && ./bundle.exe;']
    image: 'ubuntu:latest'
    principalId: userAssignedIdentity.id
  }
}

output name string = containerAppJob.name
