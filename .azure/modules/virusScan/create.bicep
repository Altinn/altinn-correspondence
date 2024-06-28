param location string
@secure()
param namePrefix string
@secure()
param storageAccountName string
@secure()
param containerAppIngress string

resource eventgrid_topic 'Microsoft.EventGrid/topics@2022-06-15' = {
  name: '${namePrefix}-malware-scan-event-topic'
  location: location
}

resource eventgrid_event_subscription 'Microsoft.EventGrid/topics/eventSubscriptions@2022-06-15' = {
  name: '${namePrefix}-malware-scan-event-subscription'
  parent: eventgrid_topic
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: 'https://${containerAppIngress}/correspondence/api/v1/webhooks/malwarescanresults'
      }
    }
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource malwareScanSettings 'Microsoft.Security/defenderForStorageSettings@2022-12-01-preview' = {
  name: 'current'
  scope: storageAccount
  properties: {
    isEnabled: true
    malwareScanning: {
      onUpload: {
        capGBPerMonth: -1
        isEnabled: true
      }
      scanResultsEventGridTopicResourceId: eventgrid_topic.id
    }
    overrideSubscriptionLevelSettings: true
    sensitiveDataDiscovery: {
      isEnabled: false
    }
  }
}
