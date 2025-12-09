targetScope = 'subscription'

param environment string

resource correspondenceTagsPolicy 'Microsoft.Authorization/policyDefinitions@2025-03-01' = {
  name: 'correspondence-standard-tags-${environment}'
  properties: {
    policyType: 'Custom'
    mode: 'Indexed'
    displayName: 'Ensure standard tags on Correspondence resources'
    description: 'Adds or updates standard FinOps and repository tags on Correspondence resource groups and resources.'
    metadata: {
      category: 'Tags'
    }
    policyRule: {
      if: {
        allOf: [
          {
            field: 'type'
            notEquals: 'Microsoft.Authorization/policyAssignments'
          }
          {
            anyOf: [
              {
                field: 'tags[finops_environment]'
                exists: 'false'
              }
              {
                field: 'tags[finops_product]'
                exists: 'false'
              }
              {
                field: 'tags[finops_serviceownercode]'
                exists: 'false'
              }
              {
                field: 'tags[finops_serviceownerorgnr]'
                exists: 'false'
              }
              {
                field: 'tags[repository]'
                exists: 'false'
              }
              {
                field: 'tags[env]'
                exists: 'false'
              }
              {
                field: 'tags[product]'
                exists: 'false'
              }
              {
                field: 'tags[org]'
                exists: 'false'
              }
            ]
          }
        ]
      }
      then: {
        effect: 'modify'
        details: {
          roleDefinitionIds: [
            '/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c' // Contributor
          ]
          operations: [
            {
              operation: 'add'
              field: 'tags[finops_environment]'
              value: environment
            }
            {
              operation: 'add'
              field: 'tags[finops_product]'
              value: 'melding'
            }
            {
              operation: 'add'
              field: 'tags[finops_serviceownercode]'
              value: 'digdir'
            }
            {
              operation: 'add'
              field: 'tags[finops_serviceownerorgnr]'
              value: '991825827'
            }
            {
              operation: 'add'
              field: 'tags[repository]'
              value: 'https://github.com/Altinn/altinn-correspondence'
            }
            {
              operation: 'add'
              field: 'tags[env]'
              value: environment
            }
            {
              operation: 'add'
              field: 'tags[product]'
              value: 'melding'
            }
            {
              operation: 'add'
              field: 'tags[org]'
              value: 'digdir'
            }
          ]
        }
      }
    }
  }
}

output policyDefinitionId string = correspondenceTagsPolicy.id


