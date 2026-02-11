using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')

// secrets
param tenantId = readEnvironmentVariable('TENANT_ID')
param test_client_id = readEnvironmentVariable('TEST_CLIENT_ID')
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME')
param maintenanceAdGroupId = readEnvironmentVariable('MAINTENANCE_AD_GROUP_ID')
param maintenanceAdGroupName = readEnvironmentVariable('MAINTENANCE_AD_GROUP_NAME')
param grafanaMonitoringPrincipalId = readEnvironmentVariable('GRAFANA_MONITORING_PRINCIPAL_ID')
param deploymentPrincipalId = readEnvironmentVariable('DEPLOYMENT_PRINCIPAL_ID')
