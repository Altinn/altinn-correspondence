using Altinn.Correspondence.Core.Services;

using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.IpSecurityRestrictionsUpdater;

public class IpSecurityRestrictionUpdater
{
    private readonly IResourceManager _azureResourceManagerService;
    
    private readonly ILogger<IpSecurityRestrictionUpdater> _logger;

    public IpSecurityRestrictionUpdater(IResourceManager azureResourceManagerService, ILogger<IpSecurityRestrictionUpdater> logger)
    {
        _azureResourceManagerService = azureResourceManagerService;
        _logger = logger;
    }

    public async Task UpdateIpRestrictions()
    {
        _logger.LogInformation("Updating IP restrictions for container app");
        var newIps = await _azureResourceManagerService.RetrieveCurrentIpRanges(CancellationToken.None);
        if (newIps.Count < 1)
        {
            _logger.LogError("Failed to retrieve current IP ranges, canceling update of IP restrictions");
            return;
        }
        await _azureResourceManagerService.UpdateContainerAppIpRestrictionsAsync(newIps, CancellationToken.None);
    }
}
