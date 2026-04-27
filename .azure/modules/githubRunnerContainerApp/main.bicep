param location string
@secure()
param namePrefix string
@secure()
param userAssignedIdentityResourceId string
@secure()
param keyVaultUrl string
@secure()
param containerAppEnvId string

@description('Container image for the self-hosted runner.')
param runnerImage string = 'ghcr.io/altinn/altinn-correspondence-github-runner:latest'
@description('GitHub registration URL, e.g. https://github.com/Altinn/altinn-correspondence')
param githubUrl string
@description('Key Vault secret name holding the GitHub PAT/token.')
param githubTokenSecretName string = 'github-runner-token'
@description('How many queued jobs each replica should target before scaling.')
param targetQueueLength int = 1
@description('Idle time in seconds before scaling down.')
param cooldownPeriodSeconds int = 3600
@description('Polling interval in seconds for scaler.')
param pollingIntervalSeconds int = 30
@description('Maximum replica count during high load.')
param maxReplicas int = 4
@description('Runner resources.')
param containerAppResources object = {
  cpu: json('1.0')
  memory: '2.0Gi'
}

var containerAppName = '${namePrefix}-github-runner'
var githubTokenSecretRefName = 'github-runner-token'

var secrets = [
  {
    identity: userAssignedIdentityResourceId
    keyVaultUrl: '${keyVaultUrl}/secrets/${githubTokenSecretName}'
    name: githubTokenSecretRefName
  }
]

var containerAppEnvVars = [
  { name: 'RUNNER_NAME_PREFIX', value: '${namePrefix}-runner' }
  { name: 'RUNNER_SCOPE', value: 'repo' }
  { name: 'GITHUB_URL', value: githubUrl }
  { name: 'DISABLE_AUTO_UPDATE', value: 'true' }
]

resource githubRunnerContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  tags: resourceGroup().tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityResourceId}': {}
    }
  }
  properties: {
    environmentId: containerAppEnvId
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: secrets
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: maxReplicas
        cooldownPeriod: cooldownPeriodSeconds
        pollingInterval: pollingIntervalSeconds
        rules: [
          {
            name: 'github-runner-queue'
            custom: {
              type: 'github-runner'
              metadata: {
                githubApiURL: 'https://api.github.com'
                owner: split(replace(githubUrl, 'https://github.com/', ''), '/')[0]
                repos: split(replace(githubUrl, 'https://github.com/', ''), '/')[1]
                targetWorkflowQueueLength: string(targetQueueLength)
                runnerScope: 'repo'
              }
              auth: [
                {
                  secretRef: githubTokenSecretRefName
                  triggerParameter: 'personalAccessToken'
                }
              ]
            }
          }
        ]
      }
      containers: [
        {
          name: 'github-runner'
          image: runnerImage
          env: concat(containerAppEnvVars, [
            {
              name: 'GITHUB_TOKEN'
              secretRef: githubTokenSecretRefName
            }
          ])
          resources: containerAppResources
        }
      ]
    }
  }
}

output name string = githubRunnerContainerApp.name
output revisionName string = githubRunnerContainerApp.properties.latestRevisionName
output app object = githubRunnerContainerApp
