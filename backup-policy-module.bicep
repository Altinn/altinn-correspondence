@description('Name of the backup policy')
param policyName string

@description('Name of the backup vault')
param vaultName string

// Reference to the existing vault in the same scope
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
            repeatingTimeIntervals: [
              'R/2024-01-07T01:00:00+00:00/P1W'
            ]
            timeZone: 'W. Europe Standard Time'
          }
        }
        lifecycles: [
          {
            deleteAfter: {
              duration: 'P12M'
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
