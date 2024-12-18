param principal_id string
param location string
@secure()
param namePrefix string
@secure()
param keyVaultName string

resource redis 'Microsoft.Cache/redis@2024-11-01' = {
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principal_id}': {}
    }
  }
  location: location
  name: '${namePrefix}-redis'
  properties: {
    disableAccessKeyAuthentication: true
    sku: {
      capacity: 0
      family: 'C'
      name: 'Standard'
    }
  }
}

var redisConnectionStringName = 'redis-connection-string'
module storageAccountConnectionStringSecret '../keyvault/upsertSecret.bicep' = {
  name: redisConnectionStringName
  params: {
    destKeyVaultName: keyVaultName
    secretName: redisConnectionStringName
    secretValue: '${namePrefix}-redis.redis.cache.windows.net,abortConnect=false,ssl=true,password=${redis.listKeys().primaryKey}'
  }
}
