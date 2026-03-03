param location string
param name string
param image string
param containerAppEnvId string
param command string[]
param environmentVariables { name: string, value: string?, secretRef: string? }[] = []
param secrets { name: string, keyVaultUrl: string, identity: string }[] = []
param volumes { name: string, storageName: string, storageType: string, mountOptions: string }[] = []
param volumeMounts { mountPath: string, subPath: string, volumeName: string }[] = []
param principalId string
param replicaTimeout int = 5400
@allowed([
  'Manual'
  'Schedule'
])
param triggerType string = 'Manual'
param cronExpression string = '0 0 * * *'
param parallelism int = 1
param replicaCompletionCount int = 1

resource job 'Microsoft.App/jobs@2023-11-02-preview' = {
  name: name
  location: location
  tags: resourceGroup().tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principalId}': {}
    }
  }
  properties: {
    configuration: triggerType == 'Schedule'
      ? {
          secrets: secrets
          scheduleTriggerConfig: {
            cronExpression: cronExpression
            parallelism: parallelism
            replicaCompletionCount: replicaCompletionCount
          }
          replicaRetryLimit: 1
          replicaTimeout: replicaTimeout
          triggerType: triggerType
        }
      : {
          secrets: secrets
          manualTriggerConfig: {
            parallelism: parallelism
            replicaCompletionCount: replicaCompletionCount
          }
          replicaRetryLimit: 1
          replicaTimeout: replicaTimeout
          triggerType: triggerType
        }
    environmentId: containerAppEnvId
    template: {
      containers: [
        {
          env: environmentVariables
          image: image
          name: name
          command: command
          volumeMounts: volumeMounts
        }
      ]
      volumes: volumes
    }
  }
}

output name string = job.name
