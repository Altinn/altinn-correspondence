using Altinn.Correspondence.Core.Models.Entities;
using Azure.ResourceManager.Network.Models;

namespace Altinn.Correspondence.Core.Services;

public interface IResourceManager
{
    Task Deploy(ServiceOwnerEntity serviceOwnerEntity, bool virusScan, CancellationToken cancellationToken);
    void CreateStorageProviders(ServiceOwnerEntity serviceOwnerEntity, CancellationToken cancellationToken);
    Task UpdateContainerAppIpRestrictionsAsync(Dictionary<string, string> newIps, CancellationToken cancellationToken);
    Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken);
    Task<Dictionary<string, string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken);
}
