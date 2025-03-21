using './main.bicep'

param azureNamePrefix = readEnvironmentVariable('AZURE_NAME_PREFIX')
param location = 'Norway East'
param containerImage = readEnvironmentVariable('containerImage')
param keyVaultName = readEnvironmentVariable('AZURE_ENVIRONMENT_KEY_VAULT_NAME')
param appRegistrationId = readEnvironmentVariable('AZURE_APP_REGISTRATION_ID')
param tenantId = readEnvironmentVariable('AZURE_TENANT_ID')
param allowedGroupId = readEnvironmentVariable('DASHBOARD_ALLOWED_GROUP_ID')
param appRegistrationClientSecret = readEnvironmentVariable('AZURE_APP_REGISTRATION_CLIENT_SECRET')
