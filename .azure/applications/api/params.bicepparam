using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param imageTag = readEnvironmentVariable('IMAGE_TAG')
param platform_base_url = 'https://platform.tt02.altinn.no/'
param environment = readEnvironmentVariable('ENVIRONMENT')
// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param client_id = readEnvironmentVariable('CLIENT_ID')
param tenant_id = readEnvironmentVariable('TENANT_ID')
