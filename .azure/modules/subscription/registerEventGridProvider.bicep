targetScope = 'subscription'

resource eventGridProviderRegistration 'Microsoft.ResourceProvider/register@2022-09-01' = {
  name: 'Microsoft.EventGrid'
}
