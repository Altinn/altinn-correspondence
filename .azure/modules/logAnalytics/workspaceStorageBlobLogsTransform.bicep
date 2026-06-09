@description('Resource ID of the Log Analytics workspace that receives StorageBlobLogs.')
param workspaceResourceId string

@description('Azure region for the workspace transformation DCR.')
param location string = resourceGroup().location

@description('Object ID of the app managed identity to exclude from StorageBlobLogs.')
param appObjectId string

@description('Prefix used for uniquely named DCR resources in this environment.')
param namePrefix string

var workspaceName = last(split(workspaceResourceId, '/'))
var transformDcrName = '${namePrefix}-storageblob-logs-transform-dcr'
var dcrAssociationName = '${namePrefix}-storageblob-logs-transform-assoc'
var logAnalyticsDestinationName = 'audit-logs'
var blobLogsTransformKql = 'source | where RequesterObjectId != "${appObjectId}"'

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
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
          'Microsoft-StorageBlobLogs'
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

resource dcrAssociation 'Microsoft.Insights/dataCollectionRuleAssociations@2023-03-11' = {
  scope: workspace
  name: dcrAssociationName
  properties: {
    dataCollectionRuleId: transformDcr.id
    description: 'Exclude app managed identity from StorageBlobLogs in the audit workspace'
  }
}

output dataCollectionRuleId string = transformDcr.id
