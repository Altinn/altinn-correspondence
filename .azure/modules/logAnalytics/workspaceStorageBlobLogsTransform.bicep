@description('Azure region for the workspace transformation DCR.')
param location string = resourceGroup().location

@description('Object ID of the app managed identity to exclude from StorageBlobLogs.')
param appObjectId string

@description('Application (client) ID of the app managed identity to exclude from StorageBlobLogs.')
param appClientId string

@description('Prefix used for uniquely named DCR resources in this environment.')
param namePrefix string

var workspaceName = '${namePrefix}-audit-logs'
var transformDcrName = '${namePrefix}-storageblob-logs-transform-dcr'
var logAnalyticsDestinationName = 'audit-logs'
var defenderScannerObjectId = storageDataScanner.identity.principalId
var blobLogsTransformKql = 'source | where AuthenticationType !~ \'TrustedAccess\' and AuthenticationType !~ \'AnonymousPreflight\' | where tostring(RequesterObjectId) !~ \'${appObjectId}\' and tostring(RequesterObjectId) !~ \'${defenderScannerObjectId}\' | where tostring(RequesterAppId) !~ \'${appClientId}\''

resource storageDataScanner 'Microsoft.Security/datascanners@2021-12-01-preview' existing = {
  scope: subscription()
  name: 'StorageDataScanner'
}

resource transformDcr 'Microsoft.Insights/dataCollectionRules@2023-03-11' = {
  name: transformDcrName
  location: location
  kind: 'WorkspaceTransforms'
  properties: {
    dataSources: {}
    dataFlows: [
      {
        streams: [
          'Microsoft-Table-StorageBlobLogs'
        ]
        destinations: [
          logAnalyticsDestinationName
        ]
        transformKql: blobLogsTransformKql
      }
    ]
    destinations: {
      logAnalytics: [
        {
          workspaceResourceId: resourceId('Microsoft.OperationalInsights/workspaces', workspaceName)
          name: logAnalyticsDestinationName
        }
      ]
    }
  }
}

// Workspace transform DCRs must be linked on the workspace itself (not via dataCollectionRuleAssociations).
resource auditLogAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  properties: {
    defaultDataCollectionRuleResourceId: transformDcr.id
  }
  dependsOn: [
    transformDcr
  ]
}

output dataCollectionRuleId string = transformDcr.id
output transformKql string = blobLogsTransformKql
output defenderScannerObjectId string = defenderScannerObjectId
