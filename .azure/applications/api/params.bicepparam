using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param imageTag = readEnvironmentVariable('IMAGE_TAG')
param platform_base_url = readEnvironmentVariable('PLATFORM_BASE_URL')
param correspondenceBaseUrl = readEnvironmentVariable('CORRESPONDENCE_BASE_URL')
param sblBridgeBaseUrl = readEnvironmentVariable('SBL_BRIDGE_BASE_URL')
param contactReservationRegistryBaseUrl = readEnvironmentVariable('CONTACT_RESERVATION_REGISTRY_BASE_URL')
param environment = readEnvironmentVariable('ENVIRONMENT')
param maskinporten_environment = readEnvironmentVariable('MASKINPORTEN_ENVIRONMENT')
param dialogportenIssuer = readEnvironmentVariable('DIALOGPORTEN_ISSUER')
param idportenIssuer = readEnvironmentVariable('IDPORTEN_ISSUER')
param maskinporten_token_exchange_environment = readEnvironmentVariable('MASKINPORTEN_TOKEN_EXCHANGE_ENVIRONMENT')
param apimIp = readEnvironmentVariable('APIM_IP')
// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME')
