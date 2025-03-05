param location string
param containerImage string
param azureNamePrefix string
param keyVaultName string
param appRegistrationId string
param appRegistrationClientSecret string
param tenantId string
param allowedGroupId string

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: keyVaultName
}

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: '${azureNamePrefix}-app-identity'
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${azureNamePrefix}-dashboard'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    environmentId: resourceId('Microsoft.App/managedEnvironments', '${azureNamePrefix}-env')
    configuration: {
      ingress: {
        external: true
        targetPort: 2526
        allowInsecure: false
      }
      secrets: [
        {
          name: 'correspondence-migration-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/correspondence-migration-connection-string'
          identity: userAssignedIdentity.id
        }
        {
          name: 'app-registration-client-secret'
          value: appRegistrationClientSecret
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'dashboard'
          image: containerImage
          env: [
            {
              name: 'DatabaseOptions__ConnectionString'
              secretRef: 'correspondence-migration-connection-string'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: userAssignedIdentity.properties.clientId
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// Configure authentication for Container App
resource containerAppAuth 'Microsoft.App/containerApps/authConfigs@2023-05-01' = {
  name: 'current'
  parent: containerApp
  properties: {
    platform: {
      enabled: true
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        login: {
          disableWWWAuthenticate: false
        }
        registration: {
          clientId: appRegistrationId
          clientSecretSettingName: 'app-registration-client-secret'
          openIdIssuer: 'https://sts.windows.net/${tenantId}/'
        }
        validation: {
          allowedAudiences: [
            'api://${appRegistrationId}'
          ]
          defaultAuthorizationPolicy: {
            allowedPrincipals: {
              groups: [
                allowedGroupId
              ]
              identities: [
                allowedGroupId
              ]
            }
            allowedApplications: [
              '${appRegistrationId}'
            ]
          }
          jwtClaimChecks: {
            allowedClientApplications: [
              '${appRegistrationId}'
            ]
            allowedGroups: [
              allowedGroupId
            ]
          }
        }
      }
    }
  }
}
