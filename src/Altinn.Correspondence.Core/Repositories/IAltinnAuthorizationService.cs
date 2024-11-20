using Altinn.Correspondence.Core.Models.Enums;
using System.Security.Claims;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAuthorizationService
{
    Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default, string? onBehalfOf = null, string? correspondenceId = null);
    Task<int?> CheckUserAccessAndGetMinimumAuthLevel(ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string recipientOrgNo, CancellationToken cancellationToken = default);
    Task<bool> CheckMigrationAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default);
}
