param location string
param environment string
param containerAppEnvironmentName string
param containerImage string
param minReplicas int = 1
param maxReplicas int = 1

module containerApp '../modules/containerApp/dashboard.bicep' = {
  name: 'dashboard'
  params: {
    name: 'altinn-corr-${environment}-dashboard'
    location: location
    environmentName: containerAppEnvironmentName
    containerImage: containerImage
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    env: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: environment
      }
    ]
  }
}

output dashboardUrl string = containerApp.outputs.url 
