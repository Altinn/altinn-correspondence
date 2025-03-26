param location string

@secure()
param principal_id string

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
      param([string] $location)
      try {
        $serviceTags = Get-AzNetworkServiceTag -Location $location
        $EventgridIps = $serviceTags.Values | Where-Object { $_.Name -eq "AzureEventGrid" }
        
        if ($EventgridIps -eq $null) {
          throw "AzureEventGrid service tag not found for the location: $location"
        }

        $output = $EventgridIps.Properties.AddressPrefixes | Where-Object { $_ -notmatch ":" }
        
        $DeploymentScriptOutputs = @{
          'eventGridIps' = $output
        }
        
        return $DeploymentScriptOutputs
      } catch {
        # Catch any errors and output them
        $DeploymentScriptOutputs = @{
          'errorMessage' = $_.Exception.Message
        }
        return $DeploymentScriptOutputs
      }
    '''
    arguments: '-location ${location}'
    forceUpdateTag: '1'
    retentionInterval: 'PT2H'
  }
}

output eventGridIps array = deploymentScript.properties.outputs.eventGridIps
