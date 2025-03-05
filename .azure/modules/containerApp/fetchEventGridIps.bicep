param location string

@secure()
param principal_id string

@secure()
param subscriptionId string

resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'fetchAzureEventGridIpsScript'
  location: location
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${principal_id}': {}
    }
  }
  properties: {
    azPowerShellVersion: '13.0'
    scriptContent: '''
      param([string] $location, [string] $subscriptionId)
      Set-AzContext -Subscription $subscriptionId
      $serviceTags = Get-AzNetworkServiceTag -Location $location
      $EventgridIps = $serviceTags.Values | Where-Object { $_.Name -eq "AzureEventGrid" }
      $output = $EventgridIps.Properties.AddressPrefixes | Where-Object { $_ -notmatch ":" }
      $DeploymentScriptOutputs = @{}
      $DeploymentScriptOutputs['eventGridIps'] = $output
    '''
    arguments: '-location ${location} - subscriptionId ${subscriptionId}'
    forceUpdateTag: '1'
    retentionInterval: 'PT2H'
  }
}

output eventGridIps array = deploymentScript.properties.outputs.eventGridIps
