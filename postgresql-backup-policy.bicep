@description('Name of the backup policy')
param policyName string

@description('Name of the backup vault')
param vaultName string

@description('Resource group where the vault is located')
param vaultResourceGroup string

@description('Subscription ID where the vault is located')
param vaultSubscriptionId string

// Create backup policy using module to handle cross-scope deployment
module backupPolicyModule './backup-policy-module.bicep' = {
  name: 'backup-policy-deployment'
  scope: resourceGroup(vaultSubscriptionId, vaultResourceGroup)
  params: {
    policyName: policyName
    vaultName: vaultName
  }
}
