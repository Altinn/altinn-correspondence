targetScope = 'resourceGroup'

param policyDefinitionId string

resource correspondenceTagsAssignment 'Microsoft.Authorization/policyAssignments@2021-06-01' = {
  name: 'correspondence-standard-tags'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'Ensure standard tags on Correspondence resources'
    policyDefinitionId: policyDefinitionId
    enforcementMode: 'Default'
  }
}

resource correspondenceTagsAssignmentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'correspondence-standard-tags-contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c') // Contributor
    principalId: correspondenceTagsAssignment.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
