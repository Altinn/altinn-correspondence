using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param imageTag = readEnvironmentVariable('IMAGE_TAG')
param platform_base_url = readEnvironmentVariable('PLATFORM_BASE_URL')
param override_authorization_url = readEnvironmentVariable('OVERRIDE_AUTHORIZATION_URL')
param override_authorization_thumbprint = readEnvironmentVariable('OVERRIDE_AUTHORIZATION_THUMBPRINT')
param correspondenceBaseUrl = readEnvironmentVariable('CORRESPONDENCE_BASE_URL')
param sblBridgeBaseUrl = readEnvironmentVariable('SBL_BRIDGE_BASE_URL')
param contactReservationRegistryBaseUrl = readEnvironmentVariable('CONTACT_RESERVATION_REGISTRY_BASE_URL')
param brregBaseUrl = readEnvironmentVariable('BRREG_BASE_URL')
param environment = readEnvironmentVariable('ENVIRONMENT')
param maskinporten_environment = readEnvironmentVariable('MASKINPORTEN_ENVIRONMENT')
param dialogportenIssuer = readEnvironmentVariable('DIALOGPORTEN_ISSUER')
param idportenIssuer = readEnvironmentVariable('IDPORTEN_ISSUER')
param maskinporten_token_exchange_environment = readEnvironmentVariable('MASKINPORTEN_TOKEN_EXCHANGE_ENVIRONMENT')
param apimIp = readEnvironmentVariable('APIM_IP')
param migrationWorkerCountPerReplica = readEnvironmentVariable('MIGRATION_WORKER_COUNT_PER_REPLICA')
param arbeidsflateOriginsCommaSeparated = readEnvironmentVariable('ARBEIDSFLATE_ORIGINS_COMMA_SEPARATED')
// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME')
