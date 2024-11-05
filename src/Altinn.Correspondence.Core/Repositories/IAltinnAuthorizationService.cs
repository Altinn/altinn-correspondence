using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAuthorizationService
{
    Task<bool> CheckUserAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default, string? recipientOrgNo = null);
    Task<int?> CheckUserAccessAndGetMinimumAuthLevel(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default, string? recipientOrgNo = null);
    Task<bool> CheckMigrationAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default);
}
