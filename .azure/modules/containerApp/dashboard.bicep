param location string
param containerImage string
@secure()
param azureNamePrefix string
@secure()
param keyVaultName string
@secure()
param appRegistrationId string
@secure()
param appRegistrationClientSecret string
@secure()
param tenantId string
@secure()
param allowedGroupId string
@secure()
param storageAccountName string
@secure()
param userAssignedIdentityId string
@secure()
param userAssignedIdentityClientId string
@secure()
param userAssignedIdentityPrincipalId string

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' existing = {
  name: 'default'
  parent: storageAccount
}

// Create a container for token storage
resource tokenContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: 'auth-tokens'
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${azureNamePrefix}-dashboard'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
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
          identity: userAssignedIdentityId
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
              value: userAssignedIdentityClientId
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

// Configure authentication for Container App with updated token store
resource containerAppAuth 'Microsoft.App/containerApps/authConfigs@2024-10-02-preview' = {
  name: 'current'
  parent: containerApp
  properties: {
    globalValidation: {
      redirectToProvider: 'AzureActiveDirectory'
      unauthenticatedClientAction: 'RedirectToLoginPage'
    }
    platform: {
      enabled: true
    }
    login: {
      tokenStore: {
        enabled: true
        azureBlobStorage: {
          blobContainerUri: 'https://${storageAccountName}.blob.${environment().suffixes.storage}/${tokenContainer.name}'
          managedIdentityResourceId: userAssignedIdentityId
        }
      }
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
          openIdIssuer: 'https://sts.windows.net/${tenantId}/v2.0'
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

// Role assignment to allow the managed identity to access the storage container
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, userAssignedIdentityId, 'StorageBlobDataContributor')
  scope: tokenContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: userAssignedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
