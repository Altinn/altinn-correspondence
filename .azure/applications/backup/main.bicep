param namePrefix string
param location string
@secure()
param tenantId string

@secure()
param storageAccountName string
param backupFileShareName string = 'correspondence-backups'

param postgresServerName string = '${namePrefix}-dbserver'
param databaseName string = 'correspondence'
param containerAppEnvName string = '${namePrefix}-env'
param backupJobName string = '${namePrefix}-backup'

param aadPropagationWaitSeconds int = 120

param pgDumpExcludeArgs string = '--exclude-table=cron.job --exclude-table=cron.job_run_details --exclude-table=__yuniql_schema_version --exclude-table=__yuniql_schema_version_sequence_id_seq'

var backupIdentityName = '${namePrefix}-backup-identity'
var backupStorageName = 'correspondence-backups'
var pgDumpExcludeArgsValue = pgDumpExcludeArgs

resource backupIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: backupIdentityName
  location: location
}

resource aadPropagationWait 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: '${backupJobName}-aad-wait'
  location: location
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${backupIdentity.id}': {}
    }
  }
  properties: {
    azPowerShellVersion: '13.0'
    scriptContent: '''
      param([int] $seconds)
      Start-Sleep -Seconds $seconds
    '''
    arguments: '-seconds ${aadPropagationWaitSeconds}'
    forceUpdateTag: '1'
    retentionInterval: 'PT2H'
  }
  dependsOn: [
    backupIdentity
  ]
}

module databaseAccess '../../modules/postgreSql/addAdminAccess.bicep' = {
  name: 'databaseAccess'
  dependsOn: [
    aadPropagationWait
  ]
  params: {
    tenantId: tenantId
    principalId: backupIdentity.properties.principalId
    appName: backupIdentity.name
    namePrefix: namePrefix
    principalType: 'ServicePrincipal'
  }
}

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-11-02-preview' existing = {
  name: containerAppEnvName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource storageAccountFileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource backupFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: backupFileShareName
  parent: storageAccountFileServices
}

resource backupEnvironmentStorage 'Microsoft.App/managedEnvironments/storages@2023-11-02-preview' = {
  name: backupStorageName
  parent: containerAppEnv
  properties: {
    azureFile: {
      accessMode: 'ReadWrite'
      accountKey: storageAccount.listKeys().keys[0].value
      accountName: storageAccountName
      shareName: backupFileShareName
    }
  }
}

var containerAppEnvVars = [
  { name: 'AZURE_CLIENT_ID', value: backupIdentity.properties.clientId }
  { name: 'PGHOST', value: '${postgresServerName}.postgres.database.azure.com' }
  { name: 'PGDATABASE', value: databaseName }
  { name: 'PGUSER', value: backupIdentity.name }
  { name: 'PGSSLMODE', value: 'require' }
]

var volumes = [
  {
    name: backupStorageName
    storageName: backupStorageName
    storageType: 'AzureFile'
    mountOptions: 'cache=none'
  }
]

var volumeMounts = [
  {
    volumeName: backupStorageName
    mountPath: '/backups'
    subPath: ''
  }
]

var commandScript = 'set -euo pipefail; apt-get update; apt-get install -y wget ca-certificates gnupg lsb-release; wget -qO - https://www.postgresql.org/media/keys/ACCC4CF8.asc | apt-key add -; release=$(lsb_release -cs); echo "deb http://apt.postgresql.org/pub/repos/apt/ $release-pgdg main" | tee /etc/apt/sources.list.d/pgdg.list; apt-get update; apt-get install -y postgresql-client-16; az login --identity --username $AZURE_CLIENT_ID > /dev/null; TOKEN=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv); export PGPASSWORD="$TOKEN"; filename="correspondence_$(date +"%Y-%m-%d_%H-%M").backup"; pg_dump -h $PGHOST -U $PGUSER -d $PGDATABASE ${pgDumpExcludeArgsValue} -Fc -f /backups/$filename --no-owner --no-privileges --no-tablespaces --quote-all-identifiers'

module containerAppJob '../../modules/migrationJob/main.bicep' = {
  name: backupJobName
  dependsOn: [
    backupEnvironmentStorage
    databaseAccess
    backupFileShare
  ]
  params: {
    name: backupJobName
    location: location
    containerAppEnvId: containerAppEnv.id
    environmentVariables: containerAppEnvVars
    secrets: []
    command: ['/bin/bash', '-c', commandScript]
    image: 'mcr.microsoft.com/azure-cli:ubuntu'
    volumes: volumes
    volumeMounts: volumeMounts
    principalId: backupIdentity.id
    replicaTimeout: 21600
  }
}

output name string = containerAppJob.outputs.name
