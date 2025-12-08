targetScope = 'resourceGroup'

param policyDefinitionId string

resource correspondenceTagsAssignment 'Microsoft.Authorization/policyAssignments@2025-03-01' = {
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
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4a9ae827-6dc8-4573-8ac7-8239d42aa03f') // Tag Contributor
    principalId: correspondenceTagsAssignment.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
