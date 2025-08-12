param namePrefix string
param location string
param environmentKeyVaultName string
param srcSecretName string

@secure()
param srcKeyVault object

@secure()
param administratorLoginPassword string
@secure()
param tenantId string

param prodLikeEnvironment bool
param logAnalyticsWorkspaceId string = ''


var databaseName = 'correspondence'
var databaseUser = 'adminuser'
var poolSize = prodLikeEnvironment ? 100 : 25

module saveAdmPassword '../keyvault/upsertSecret.bicep' = {
  name: 'Save_${srcSecretName}'
  scope: resourceGroup(srcKeyVault.subscriptionId, srcKeyVault.resourceGroupName)
  params: {
    destKeyVaultName: srcKeyVault.name
    secretName: srcSecretName
    secretValue: administratorLoginPassword
  }
}

var migrationConnectionStringName = 'correspondence-migration-connection-string'
module saveMigrationConnectionString '../keyvault/upsertSecret.bicep' = {
  name: 'Save_${migrationConnectionStringName}'
  scope: resourceGroup(srcKeyVault.subscriptionId, srcKeyVault.resourceGroupName)
  params: {
    destKeyVaultName: srcKeyVault.name
    secretName: migrationConnectionStringName
    secretValue: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Port=5432;Username=${databaseUser};Password=${administratorLoginPassword};'
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${namePrefix}-dbserver'
  location: location
  properties: {
    version: '16'
    administratorLogin: databaseUser
    administratorLoginPassword: administratorLoginPassword
    storage: {
      storageSizeGB: prodLikeEnvironment ? 4096 : 32
      autoGrow: 'Enabled'
      tier: prodLikeEnvironment ? 'P50' : 'P4'
    }
    backup: { backupRetentionDays: 35 }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: tenantId
    }
  }
  sku: prodLikeEnvironment
    ? {
        name: 'Standard_D8ads_v5'
        tier: 'GeneralPurpose'
      }
    : {
        name: 'Standard_B1ms'
        tier: 'Burstable'
      }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: databaseName
  parent: postgres
  properties: {
    charset: 'UTF8'
    collation: 'nb_NO.utf8'
  }
}

resource extensionsConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'azure.extensions'
  parent: postgres
  dependsOn: [database]
  properties: {
    value: 'UUID-OSSP,HSTORE,PG_CRON,PG_STAT_STATEMENTS'
    source: 'user-override'
  }
}

resource maxConnectionsConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'max_connections'
  parent: postgres
  dependsOn: [database, extensionsConfiguration]
  properties: {
    value: prodLikeEnvironment ? '3000' : '50'
    source: 'user-override'
  }
}

resource workMemConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'work_mem'
  parent: postgres
  dependsOn: [database, maxConnectionsConfiguration]
  properties: {
    value: prodLikeEnvironment ? '1097151' : '4096'
    source: 'user-override'
  }
}

resource maintenanceWorkMemConfiguration 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'maintenance_work_mem'
  parent: postgres
  dependsOn: [database, workMemConfiguration]
  properties: {
    value: prodLikeEnvironment ? '2097151' : '99328'
    source: 'user-override'
  }
}

resource maxPreparedTransactions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'max_prepared_transactions'
  parent: postgres
  dependsOn: [database, maintenanceWorkMemConfiguration]
  properties: {
    value: prodLikeEnvironment ? '3000' : '50'
    source: 'user-override'
  }
}

resource maxParallellWorkers 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'max_parallel_workers'
  parent: postgres
  dependsOn: [database, maxPreparedTransactions]
  properties: {
    value: '32'
    source: 'user-override'
  }
}

resource maxParallellWorkersPerGather 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'max_parallel_workers_per_gather'
  parent: postgres
  dependsOn: [database, maxParallellWorkers]
  properties: {
    value: '16'
    source: 'user-override'
  }
}

resource parallelSetupCost 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'parallel_setup_cost'
  parent: postgres
  dependsOn: [database, maxParallellWorkersPerGather]
  properties: {
    value: '5'
    source: 'user-override'
  }
}

resource parallelTupleCost 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'parallel_tuple_cost'
  parent: postgres
  dependsOn: [database, parallelSetupCost]
  properties: {
    value: '0.05'
    source: 'user-override'
  }
}

resource sessionReplicationRole 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'session_replication_role'
  parent: postgres
  dependsOn: [database, parallelTupleCost]
  properties: {
    value: 'Replica'
    source: 'user-override'
  }
}

resource cronDatabaseName 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'cron.database_name'
  parent: postgres
  dependsOn: [database, sessionReplicationRole]
  properties: {
    value: 'correspondence'
    source: 'user-override'
  }
}

// Query Store and pg_stat_statements configurations
resource sharedPreloadLibraries 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'shared_preload_libraries'
  parent: postgres
  dependsOn: [database, cronDatabaseName]
  properties: {
    value: 'pg_stat_statements,pg_cron'
    source: 'user-override'
  }
}
resource pgStatStatementsTrack 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'pg_stat_statements.track'
  parent: postgres
  dependsOn: [database, sharedPreloadLibraries]
  properties: {
    value: 'all'
    source: 'user-override'
  }
}

resource pgStatStatementsMax 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'pg_stat_statements.max'
  parent: postgres
  dependsOn: [database, pgStatStatementsTrack]
  properties: {
    value: '10000'
    source: 'user-override'
  }
}

resource trackIoTiming 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'track_io_timing'
  parent: postgres
  dependsOn: [database, pgStatStatementsMax]
  properties: {
    value: 'on'
    source: 'user-override'
  }
}

resource pgQsQueryCaptureMode 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'pg_qs.query_capture_mode'
  parent: postgres
  dependsOn: [database, trackIoTiming]
  properties: {
    value: 'all'
    source: 'user-override'
  }
}

resource pgmsWaitSamplingQueryCaptureMode 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  name: 'pgms_wait_sampling.query_capture_mode'
  parent: postgres
  dependsOn: [database, pgQsQueryCaptureMode]
  properties: {
    value: 'all'
    source: 'user-override'
  }
}



resource allowAzureAccess 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  name: 'azure-access'
  parent: postgres
  dependsOn: [database, pgmsWaitSamplingQueryCaptureMode] // Needs to depend on database to avoid updating at the same time
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

module adoConnectionString '../keyvault/upsertSecret.bicep' = {
  name: 'adoConnectionString'
  params: {
    destKeyVaultName: environmentKeyVaultName
    secretName: 'correspondence-ado-connection-string'
    secretValue: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Port=5432;Username=${namePrefix}-app-identity;Ssl Mode=Require;Trust Server Certificate=True;Maximum Pool Size=${poolSize};options=-c role=azure_pg_admin;'
  }
}

// Diagnostic settings for Query Store and monitoring
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '') {
  name: 'QueryStoreDiagnostics'
  scope: postgres
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'PostgreSQLFlexQueryStoreRuntime'
        enabled: true
      }
      {
        category: 'PostgreSQLFlexQueryStoreWaitStats'
        enabled: true
      }
      {
        category: 'PostgreSQLFlexSessions'
        enabled: true
      }
    ]
  }
}


