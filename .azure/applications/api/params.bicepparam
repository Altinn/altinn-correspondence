using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param imageTag = readEnvironmentVariable('IMAGE_TAG')
param platform_base_url = readEnvironmentVariable('PLATFORM_BASE_URL')
param correspondenceBaseUrl = readEnvironmentVariable('CORRESPONDENCE_BASE_URL')
param environment = readEnvironmentVariable('ENVIRONMENT')
param maskinporten_environment = 'test'
param dialogportenIssuer = readEnvironmentVariable('DIALOGPORTEN_ISSUER')
param idportenIssuer = readEnvironmentVariable('IDPORTEN_ISSUER')
// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME')
