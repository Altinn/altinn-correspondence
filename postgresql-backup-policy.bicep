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
            repeatingTimeIntervals: backupRepeatingTimeIntervals
            timeZone: timeZone
          }
        }
        lifecycles: [
          {
            deleteAfter: {
              duration: defaultRetentionDuration
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
      {
        name: 'weekly'
        objectType: 'AzureRetentionRule'
        isDefault: false
        lifecycles: [
          {
            deleteAfter: {
              duration: weeklyRetentionDuration
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
        criteria: {
          objectType: 'ScheduleBasedBackupCriteria'
          absoluteCriteria: 'FirstOfWeek'
        }
      }
    ]
  }
}

// Reference to the vault resource (you'll need to define this or pass it as a parameter)
resource vaultResource 'Microsoft.DataProtection/backupVaults@2023-05-01' existing = {
  name: split(vaultId, '/')[8] // Extract vault name from full resource ID
}
