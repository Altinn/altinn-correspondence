
using Azure.ResourceManager.Network.Models;

namespace Altinn.Correspondence.Core.Services;
public interface IResourceManager
{
    Task UpdateContainerAppIpRestrictionsAsync(Dictionary<string, string> newIps, CancellationToken cancellationToken);
    Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken);
    Task<Dictionary<string, string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken);
}