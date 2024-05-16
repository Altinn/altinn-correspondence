using './main.bicep'

param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')
param keyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param appVersion = readEnvironmentVariable('APP_VERSION')
param client_id = readEnvironmentVariable('CLIENT_ID')
param tenant_id = readEnvironmentVariable('TENANT_ID')
