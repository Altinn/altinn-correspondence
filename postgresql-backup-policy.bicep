@description('Name of the backup policy')
param policyName string = 'postgresql-weekly-sunday-backup-policy-12m'

@description('ID of the backup vault')
param vaultId string

@description('Backup repeating time intervals')
param backupRepeatingTimeIntervals array = [
  'R/2024-01-07T01:00:00+00:00/P1W'
]

@description('Time zone for the backup policy')
param timeZone string = 'W. Europe Standard Time'

@description('Default retention duration')
param defaultRetentionDuration string = 'P12M'

@description('Weekly retention duration')
param weeklyRetentionDuration string = 'P12M'

// Extract scope parts and bind the existing vault to its real scope
// Expected format: /subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.DataProtection/backupVaults/{vaultName}
var vaultIdParts = split(vaultId, '/')
var vaultSubscriptionId = vaultIdParts[2]
var vaultRgName = vaultIdParts[4]
var vaultName = last(vaultIdParts)

// Note: vaultId should be in format: /subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.DataProtection/backupVaults/{vaultName}

// Deploy backup policy to the vault's scope
module backupPolicyModule 'backup-policy-module.bicep' = {
  name: 'backup-policy-deployment'
  scope: resourceGroup(vaultSubscriptionId, vaultRgName)
  params: {
    policyName: policyName
    vaultName: vaultName
    backupRepeatingTimeIntervals: backupRepeatingTimeIntervals
    timeZone: timeZone
    defaultRetentionDuration: defaultRetentionDuration
    weeklyRetentionDuration: weeklyRetentionDuration
  }
}
