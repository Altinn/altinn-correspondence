param location string
param name string
param environmentName string
param containerImage string
param minReplicas int = 1
param maxReplicas int = 1
param env array = []

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: name
  location: location
  properties: {
    environmentId: environmentName
    configuration: {
      ingress: {
        external: true
        targetPort: 2526
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'dashboard'
          image: containerImage
          env: env
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output url string = containerApp.properties.configuration.ingress.fqdn 
