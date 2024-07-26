using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAuthorizationService
{
    Task<bool> CheckUserAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default);
}
