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
    secretValue: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Port=5432;Username=${databaseUser};Password=${administratorLoginPassword};options=-c role=azure_pg_admin;'
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
      storageSizeGB: prodLikeEnvironment ? 2048 : 32
      autoGrow: 'Enabled'
      tier: prodLikeEnvironment ? 'P40': 'P4'
    }
    backup: { backupRetentionDays: 35 }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: tenantId
    }
  }
  sku: prodLikeEnvironment ? {
    name: 'Standard_D8ads_v5'
    tier: 'GeneralPurpose'
  } : {
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

resource allowAzureAccess 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  name: 'azure-access'
  parent: postgres
  dependsOn: [database, maintenanceWorkMemConfiguration] // Needs to depend on database to avoid updating at the same time
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
