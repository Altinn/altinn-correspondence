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
var dcrAssociationName = '${namePrefix}-storageblob-logs-transform-assoc'
var logAnalyticsDestinationName = 'audit-logs'
var defenderScannerObjectId = storageDataScanner.identity.principalId
var blobLogsTransformKql = 'source | where AuthenticationType !~ \'TrustedAccess\' | where tolower(tostring(RequesterObjectId)) !in~ (tolower(\'${appObjectId}\'), tolower(\'${defenderScannerObjectId}\')) | where tolower(tostring(RequesterAppId)) != tolower(\'${appClientId}\')'

resource storageDataScanner 'Microsoft.Security/datascanners@2021-12-01-preview' existing = {
  scope: subscription()
  name: 'StorageDataScanner'
}

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
          workspaceResourceId: workspace.id
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
    description: 'Exclude app managed identity, Defender StorageDataScanner, and platform TrustedAccess calls from StorageBlobLogs'
  }
}

output dataCollectionRuleId string = transformDcr.id
output transformKql string = blobLogsTransformKql
