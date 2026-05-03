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
param runnerImage string
@description('GitHub registration URL, e.g. https://github.com/Altinn/altinn-correspondence')
param githubUrl string

var containerAppName = '${namePrefix}-github-runner'
var githubTokenSecretRefName = 'github-runner-token'
var githubTokenSecretName = 'github-runner-token'
var targetQueueLength = 1
var cooldownPeriodSeconds = 3600
var pollingIntervalSeconds = 30
var maxReplicas = 4
var containerAppResources = {
  cpu: 2
  memory: '4.0Gi'
}

var secrets = [
  {
    identity: userAssignedIdentityResourceId
    keyVaultUrl: '${keyVaultUrl}/secrets/${githubTokenSecretName}'
    name: githubTokenSecretRefName
  }
]

var containerAppEnvVars = [
  { name: 'RUNNER_SCOPE', value: 'repo' }
  { name: 'GITHUB_URL', value: githubUrl }
  { name: 'DISABLE_AUTO_UPDATE', value: 'true' }
]

resource githubRunnerContainerApp 'Microsoft.App/containerApps@2024-02-02-preview' = {
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
        cooldownPeriodInSeconds: cooldownPeriodSeconds
        pollingIntervalInSeconds: pollingIntervalSeconds
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
