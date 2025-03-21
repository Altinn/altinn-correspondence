using Altinn.Correspondence.Core.Services;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Azure;
public class AzureResourceManagerService : IResourceManager
{
    private readonly AzureResourceManagerOptions _resourceManagerOptions;
    private readonly ArmClient _armClient;
    private readonly TokenCredential _credentials;
    private readonly ILogger<AzureResourceManagerService> _logger;

    private SubscriptionResource GetSubscription() => _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}"));

    public AzureResourceManagerService(IOptions<AzureResourceManagerOptions> resourceManagerOptions, ILogger<AzureResourceManagerService> logger)
    {
        _resourceManagerOptions = resourceManagerOptions.Value;
        _credentials = new DefaultAzureCredential();
        _armClient = new ArmClient(_credentials);
        _logger = logger;
    }

    public async Task UpdateContainerAppIpRestrictionsAsync(Dictionary<string, string> newIps, CancellationToken cancellationToken)
    {
        try 
        {
            var containerAppResourceId = new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{_resourceManagerOptions.ApplicationResourceGroupName}/providers/Microsoft.App/containerapps/{_resourceManagerOptions.ContainerAppName}");
            var containerApp = await _armClient.GetContainerAppResource(containerAppResourceId).GetAsync(cancellationToken);

            var ipRestrictions = containerApp.Value.Data.Configuration.Ingress.IPSecurityRestrictions;

            ipRestrictions.Clear();

            foreach (var ip in newIps)
            {
                ipRestrictions.Add(new ContainerAppIPSecurityRestrictionRule(name: $"IP whitelist {ip.Value}", action: ContainerAppIPRuleAction.Allow, ipAddressRange: ip.Key));
            }

            _logger.LogInformation("Updating IP restrictions for container app");
            var response = await containerApp.Value.UpdateAsync(waitUntil: WaitUntil.Started, data: containerApp.Value.Data, cancellationToken: cancellationToken);

            if (response.GetRawResponse().Status != 200)
            {
                _logger.LogError("Failed to update IP restrictions for container app. Status code: {StatusCode}", response.GetRawResponse().Status);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while updating IP security restrictions for container app");
        }
    }

    public async Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken)
    {
        try
        {
            Response<ServiceTagsListResult> response = await GetSubscription().GetServiceTagAsync(_resourceManagerOptions.Location, cancellationToken);
            if (response.GetRawResponse().Status != 200)
            {
                _logger.LogError("Failed to retrieve Azure service tags in Azure Resource Manager Service. Status code: {StatusCode}", response.GetRawResponse().Status);
                return null;
            }
            return response.Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while retrieving Azure service tags");
            return null;
        }
    }

    public async Task<Dictionary<string, string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken)
    {
        string serviceTagId = "AzureEventGrid";
        var serviceTagsListResult = await RetrieveServiceTags(cancellationToken);
        var retrievedAddresses = serviceTagsListResult?.Values
            .Where(v => string.Equals(v.Id, serviceTagId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(v => v.Properties.AddressPrefixes)
            .Where(ip => !ip.Contains(':'))
            .ToList();
        if (retrievedAddresses == null || retrievedAddresses.Count == 0)
        {
            _logger.LogError($"No EventGrid IP addresses were retrieved. Service tag '{serviceTagId}' may not exist.");
            return new Dictionary<string, string>();
        }
        Dictionary<string, string> addresses = retrievedAddresses.ToDictionary(ip => ip, ip => $"{serviceTagId} IP");
        addresses.Add(_resourceManagerOptions.ApimIP, "Apim IP");
        return addresses;
    }
}
