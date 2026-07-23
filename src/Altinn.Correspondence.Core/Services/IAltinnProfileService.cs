using Altinn.Correspondence.Core.Models.Profile;

namespace Altinn.Correspondence.Core.Services;

public interface IAltinnProfileService
{
    /// <summary>
    /// Looks up the contact points users have registered for themselves on the given organizations,
    /// filtered to contact points valid for the given resource.
    /// </summary>
    Task<List<UnitContactPoints>> GetUserRegisteredContactPoints(List<string> organizationNumbers, string resourceId, CancellationToken cancellationToken);

    /// <summary>
    /// Looks up the officially registered notification addresses for the given organizations.
    /// </summary>
    Task<List<OrgNotificationAddresses>> GetOrganizationNotificationAddresses(List<string> organizationNumbers, CancellationToken cancellationToken);
}
