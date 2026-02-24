targetScope = 'subscription'
@minLength(3)
param location string
@secure()
param sourceKeyVaultName string
@secure()
param tenantId string
@secure()
param test_client_id string
param environment string
param namePrefix string
@secure()
param storageAccountName string
param storageAccountSku string = 'Standard_LRS'
@secure()
param grafanaMonitoringPrincipalId string

param backupImageTag string = 'latest'


@secure()
param maintenanceAdGroupId string
@secure()
param maintenanceAdGroupName string

var prodLikeEnvironment = environment == 'production' || environment == 'staging' || environment == 'yt01'
var resourceGroupName = '${namePrefix}-rg'
var standardTags = {
  finops_environment: environment
  finops_product: 'melding'
  finops_serviceownercode: 'digdir'
  finops_serviceownerorgnr: '991825827'
  repository: 'https://github.com/Altinn/altinn-correspondence'
  env: environment
  product: 'melding'
  org: 'digdir'
}

// Create resource groups
resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: standardTags
}

module environmentKeyVault '../modules/keyvault/create.bicep' = {
  scope: resourceGroup
  name: 'keyVault'
  params: {
    vaultName: sourceKeyVaultName
    location: location
    tenant_id: tenantId
  }
}

module grantTestClientSecretsOfficerRole '../modules/keyvault/addSecretsOfficerRole.bicep' = if (environment == 'test') {
  scope: resourceGroup
  name: 'kv-secrets-officer-test-client'
  dependsOn: [ environmentKeyVault ]
  params: {
    keyvaultName: sourceKeyVaultName
    principalObjectId: test_client_id
    principalType: 'Group'
  }
}

// #####################################################
// Create resources with dependencies to other resources
// #####################################################

module storageAccount '../modules/storageAccount/create.bicep' = {
  scope: resourceGroup
  name: storageAccountName
  params: {
    storageAccountName: storageAccountName
    location: location
    fileshare: 'migrations'
    storageAccountSku: storageAccountSku
  }
}

module containerAppEnv '../modules/containerAppEnvironment/main.bicep' = {
  scope: resourceGroup
  name: 'container-app-environment'
  params: {
    keyVaultName: sourceKeyVaultName
    location: location
    namePrefix: namePrefix
    storageAccountName: storageAccountName
  }
}

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
    tenantId: tenantId
    prodLikeEnvironment: prodLikeEnvironment
    logAnalyticsWorkspaceId: containerAppEnv.outputs.logAnalyticsWorkspaceId
    auditLogAnalyticsWorkspaceId: containerAppEnv.outputs.auditLogAnalyticsWorkspaceId
    environment: environment
  }
}

module maintenanceDbAccess '../modules/postgreSql/addAdminAccess.bicep' = {
  name: 'databaseAccess'
  scope: resourceGroup
  dependsOn: [
    postgresql
  ]
  params: {
    principalType: 'Group'
    tenantId: tenantId
    principalId: maintenanceAdGroupId
    appName: maintenanceAdGroupName
    namePrefix: namePrefix
  }
}

module reddis '../modules/redis/main.bicep' = {
  scope: resourceGroup
  name: 'redis'
  params: {
    location: location
    namePrefix: namePrefix
    keyVaultName: sourceKeyVaultName
    prodLikeEnvironment: prodLikeEnvironment
    environment: environment
  }
}

module grafanaMonitoringReaderRole '../modules/subscription/addMonitoringReaderRole.bicep' = {
  name: 'grafana-monitoring-reader'
  params: {
    grafanaPrincipalId: grafanaMonitoringPrincipalId
  }
}

module backupJob '../applications/backup/main.bicep' = {
  scope: resourceGroup
  name: 'correspondence-backup-job'
  params: {
    namePrefix: namePrefix
    location: location
    tenantId: tenantId
    storageAccountName: storageAccountName
    backupImageTag: backupImageTag
  }
}

module correspondenceTagsPolicy '../modules/policy/correspondenceTagsPolicy.bicep' = {
  name: 'correspondence-standard-tags-definition'
  params: {
    environment: environment
  }
}

module correspondenceTagsAssignment '../modules/policy/assignCorrespondenceTags.bicep' = {
  name: 'correspondence-standard-tags-assignment'
  scope: resourceGroup
  params: {
    policyDefinitionId: correspondenceTagsPolicy.outputs.policyDefinitionId
    userAssignedIdentityName: '${namePrefix}-correspondence-tags-mi'
  }
}

output resourceGroupName string = resourceGroup.name
output environmentKeyVaultName string = environmentKeyVault.outputs.name
