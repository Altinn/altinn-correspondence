@description('Name of the backup policy')
param policyName string = 'my-postgresql-backup-policy'

@description('Full resource ID of the backup vault')
param vaultId string

@description('Backup schedule (ISO 8601 recurring format)')
param backupSchedule string = 'R/2024-01-07T01:00:00+00:00/P1W'

@description('Time zone for the backup policy')
param timeZone string = 'W. Europe Standard Time'

@description('Retention duration (ISO 8601 duration format)')
param retentionDuration string = 'P12M'

// Extract vault name from full resource ID
var vaultName = last(split(vaultId, '/'))

// Reference to the vault resource
resource vaultResource 'Microsoft.DataProtection/backupVaults@2023-05-01' existing = {
  name: vaultName
}

// Create backup policy
resource backupPolicy 'Microsoft.DataProtection/backupVaults/backupPolicies@2023-05-01' = {
  name: policyName
  parent: vaultResource
  properties: {
    datasourceTypes: [
      'Microsoft.DBforPostgreSQL/flexibleServers/databases'
    ]
    policyRules: [
      {
        name: 'Default'
        objectType: 'AzureBackupRule'
        backupParameters: {
          objectType: 'AzureBackupParams'
          backupType: 'Full'
        }
        trigger: {
          objectType: 'ScheduleBasedTriggerContext'
          schedule: {
            repeatingTimeIntervals: [backupSchedule]
            timeZone: timeZone
          }
        }
        lifecycles: [
          {
            deleteAfter: {
              duration: retentionDuration
              objectType: 'AbsoluteDeleteOption'
            }
            sourceDataStore: {
              dataStoreType: 'VaultStore'
              objectType: 'DataStoreInfoBase'
            }
            targetDataStore: {
              dataStoreType: 'VaultStore'
              objectType: 'DataStoreInfoBase'
            }
          }
        ]
      }
    ]
  }
}
