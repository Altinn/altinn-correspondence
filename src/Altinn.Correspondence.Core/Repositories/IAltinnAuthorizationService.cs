using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using System.Security.Claims;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAuthorizationService
{
    Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsAny(ClaimsPrincipal? user, string resource, string party, CancellationToken cancellationToken);
    Task<int?> CheckUserAccessAndGetMinimumAuthLevel(ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string recipientOrgNo, CancellationToken cancellationToken = default);
    Task<Dictionary<(string, string), int?>> CheckUserAccessAndGetMinimumAuthLevelWithMultirequest(ClaimsPrincipal? user, string ssn, List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken = default);
}
