param location string
@secure()
param namePrefix string
@secure()
param keyVaultName string
param environment string
param prodLikeEnvironment bool

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-redis-identity'
  location: location
}

resource redis 'Microsoft.Cache/redis@2024-11-01' = {
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  location: location
  name: '${namePrefix}-redis'
  properties: {
    sku: {
      capacity: prodLikeEnvironment ? 2 : environment == 'staging' ? 1 : 0
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
