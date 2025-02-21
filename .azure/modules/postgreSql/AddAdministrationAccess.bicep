param principalId string
param tenantId string
param appName string
param namePrefix string

resource database 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' existing = {
  name: '${namePrefix}-dbserver'
}
resource databaseAccess 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = {
  name: principalId
  parent: database
  properties: {
    principalType: 'ServicePrincipal'
    tenantId: tenantId
    principalName: appName
  }
}
