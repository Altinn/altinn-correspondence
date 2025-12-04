targetScope = 'resourceGroup'

param policyDefinitionId string

resource correspondenceTagsAssignment 'Microsoft.Authorization/policyAssignments@2021-06-01' = {
  name: 'correspondence-standard-tags'
  properties: {
    displayName: 'Ensure standard tags on Correspondence resources'
    policyDefinitionId: policyDefinitionId
    enforcementMode: 'Default'
  }
}
