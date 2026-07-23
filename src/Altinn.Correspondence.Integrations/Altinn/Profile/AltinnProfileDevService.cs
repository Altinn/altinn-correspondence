using Altinn.Correspondence.Core.Models.Profile;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Profile;

public class AltinnProfileDevService : IAltinnProfileService
{
    public Task<List<UnitContactPoints>> GetUserRegisteredContactPoints(List<string> organizationNumbers, string resourceId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<UnitContactPoints>());
    }

    public Task<List<OrgNotificationAddresses>> GetOrganizationNotificationAddresses(List<string> organizationNumbers, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<OrgNotificationAddresses>());
    }
}
