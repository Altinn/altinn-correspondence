using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')

// secrets
param tenantId = readEnvironmentVariable('TENANT_ID')
param test_client_id = readEnvironmentVariable('TEST_CLIENT_ID')
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME')
param maskinportenJwk = readEnvironmentVariable('MASKINPORTEN_JWK')
param maskinportenClientId = readEnvironmentVariable('MASKINPORTEN_CLIENT_ID')
param platformSubscriptionKey = readEnvironmentVariable('PLATFORM_SUBSCRIPTION_KEY')
param accessManagementSubscriptionKey = readEnvironmentVariable('ACCESS_MANAGEMENT_SUBSCRIPTION_KEY')
param slackUrl = readEnvironmentVariable('SLACK_URL')
param idportenClientId = readEnvironmentVariable('IDPORTEN_CLIENT_ID')
param idportenClientSecret = readEnvironmentVariable('IDPORTEN_CLIENT_SECRET')
param maskinporten_token_exchange_environment = readEnvironmentVariable('MASKINPORTEN_TOKEN_EXCHANGE_ENVIRONMENT')
param maintenanceAdGroupId = readEnvironmentVariable('MAINTENANCE_AD_GROUP_ID')
param maintenanceAdGroupName = readEnvironmentVariable('MAINTENANCE_AD_GROUP_NAME')
param statisticsApiKey = readEnvironmentVariable('STATISTICS_API_KEY')
