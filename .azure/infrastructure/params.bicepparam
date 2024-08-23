using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')

// secrets
param correspondencePgAdminPassword = readEnvironmentVariable('CORRESPONDENCE_PG_ADMIN_PASSWORD')
param tenantId = readEnvironmentVariable('TENANT_ID')
param test_client_id = readEnvironmentVariable('TEST_CLIENT_ID')
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME')
param maskinportenJwk = readEnvironmentVariable('MASKINPORTEN_JWK')
param maskinportenClientId = readEnvironmentVariable('MASKINPORTEN_CLIENT_ID')
param platformSubscriptionKey = readEnvironmentVariable('PLATFORM_SUBSCRIPTION_KEY')
param notificationEmail = readEnvironmentVariable('NOTIFICATION_EMAIL')
// SKUs
param keyVaultSku = {
  name: 'standard'
  family: 'A'
}
param postgresSku = {
  name: 'Standard_B1ms'
  tier: 'Burstable'
}
