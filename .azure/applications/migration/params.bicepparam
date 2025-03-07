using './main.bicep'

param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')
param keyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param appVersion = readEnvironmentVariable('APP_VERSION')
param apimIp = (environment == 'test' ? '51.120.88.69' : environment == 'staging' ? '51.13.86.131' : environment == 'production' ? '51.120.88.54' : '')
