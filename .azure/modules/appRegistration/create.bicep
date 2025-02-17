@description('The display name for the application registration')
param displayName string

resource applicationRegistration 'Microsoft.Graph/applications@2022-01-01' = {
  name: displayName
  properties: {
    displayName: displayName
    signInAudience: 'AzureADMyOrg'
  }
}

resource servicePrincipal 'Microsoft.Graph/servicePrincipals@2022-01-01' = {
  name: '${displayName}-sp'
  properties: {
    appId: applicationRegistration.properties.appId
    displayName: displayName
  }
}

output clientId string = applicationRegistration.properties.appId
output tenantId string = tenant().tenantId
