param namePrefix string
param location string
param environmentKeyVaultName string

@secure()
param tenantId string

param prodLikeEnvironment bool

var databaseName = 'correspondence'
var poolSize = prodLikeEnvironment ? 100 : 25

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${namePrefix}-dbserver'
  location: location
  properties: {
    version: '16'
    storage: {
      storageSizeGB: prodLikeEnvironment ? 2048 : 32
      autoGrow: 'Enabled'
      tier: prodLikeEnvironment ? 'P40' : 'P4'
    }
    backup: { backupRetentionDays: 35 }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
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
    value: 'UUID-OSSP,HSTORE'
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

resource allowAzureAccess 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  name: 'azure-access'
  parent: postgres
  dependsOn: [database, maxPreparedTransactions] // Needs to depend on database to avoid updating at the same time
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

module migrationConnectionString '../keyvault/upsertSecret.bicep' = {
  name: 'migrationConnectionString'
  params: {
    destKeyVaultName: environmentKeyVaultName
    secretName: 'correspondence-migration-connection-string'
    secretValue: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Port=5432;Username=${namePrefix}-migration-identity;Ssl Mode=Require;Trust Server Certificate=True;Maximum Pool Size=${poolSize};options=-c role=azure_pg_admin;'
  }
}
