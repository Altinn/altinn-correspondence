@secure()
param storageAccountName string
param fileshare string
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Cool'
    minimumTlsVersion: 'TLS1_2'
    allowSharedKeyAccess: false
  }
}

resource storageAccountFileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    protocolSettings: {
      smb: {
        authenticationMethods: 'AzureActiveDirectory'
        channelEncryption: 'AES-128-CCM;AES-128-GCM;AES-256-GCM'
        kerberosTicketEncryption: 'AES-256'
        multichannel: {
          enabled: false
        }
        versions: 'SMB3.1.1;SMB3.0;SMB2.1'
      }
    }
  }
}

resource storageAccountFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: fileshare
  parent: storageAccountFileServices
}

resource storageAccountBlobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-04-01' = {
  name: 'default'
  parent: storageAccount
}

resource storageAccountAttachmentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-04-01' = {
  name: 'attachments'
  parent: storageAccountBlobServices
}

output storageAccountId string = storageAccount.id
