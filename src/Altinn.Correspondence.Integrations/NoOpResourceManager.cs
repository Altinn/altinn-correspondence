using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Services;
using Azure.ResourceManager.Network.Models;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations;

public sealed class NoOpResourceManager(ILogger<NoOpResourceManager> logger) : IResourceManager
{
    public void DeployStorageAccountsForServiceOwner(ServiceOwnerEntity serviceOwnerEntity, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Skipping Azure storage provisioning for service owner {ServiceOwner}. Attachments use the shared S3 bucket.",
            serviceOwnerEntity.Name);
    }

    public Task DeployStorageAccount(ServiceOwnerEntity serviceOwnerEntity, bool virusScan, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task UpdateContainerAppIpRestrictionsAsync(Dictionary<string, string> newIps, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken)
    {
        return Task.FromResult<ServiceTagsListResult?>(null);
    }

    public Task<Dictionary<string, string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken)
    {
        return Task.FromResult(new Dictionary<string, string>());
    }
}
