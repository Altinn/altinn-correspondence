@description('Name of the backup policy')
param policyName string

@description('Name of the backup vault')
param vaultName string

@description('Backup repeating time intervals')
param backupRepeatingTimeIntervals array

@description('Time zone for the backup policy')
param timeZone string

@description('Default retention duration')
param defaultRetentionDuration string

@description('Weekly retention duration')
param weeklyRetentionDuration string

// Reference to the vault resource in the same scope
resource vaultResource 'Microsoft.DataProtection/backupVaults@2023-05-01' existing = {
  name: vaultName
}

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
          absoluteCriteria: [
            'FirstOfWeek'
          ]
        }
      }
    ]
  }
}
