@description('Azure region for the audit workspace and transformation DCR. Must match the existing workspace location.')
param location string

@description('Prefix used for uniquely named DCR resources in this environment.')
param namePrefix string

var workspaceName = '${namePrefix}-audit-logs'
var workspaceResourceId = resourceId('Microsoft.OperationalInsights/workspaces', workspaceName)
var transformDcrName = '${namePrefix}-storageblob-logs-transform-dcr'
var logAnalyticsDestinationName = 'audit-logs'
// Keep interactive user access only: app MI, Defender scanner, and platform calls have no RequesterUpn.
var blobLogsTransformKql = 'source | where AuthenticationType =~ \'OAuth\' | where isnotempty(RequesterUpn)'

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
          workspaceResourceId: workspaceResourceId
          name: logAnalyticsDestinationName
        }
      ]
    }
  }
}

// Workspace transform DCRs must be linked on the workspace itself (not via dataCollectionRuleAssociations).
resource auditLogAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    defaultDataCollectionRuleResourceId: transformDcr.id
  }
  dependsOn: [
    transformDcr
  ]
}

output dataCollectionRuleId string = transformDcr.id
output transformKql string = blobLogsTransformKql
