using Altinn.Correspondence.Core.Models.Entities;
using System.Security.Claims;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAuthorizationService
{
    Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default);
    Task<bool> CheckAttachmentAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, AttachmentEntity attachment, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsAny(ClaimsPrincipal? user, string resource, string party, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the subset of the given user ids that are authorized to read the resource on behalf of the organization.
    /// </summary>
    Task<List<int>> AuthorizeUserIdsForResource(int partyId, IReadOnlyCollection<int> userIds, string resourceId, CancellationToken cancellationToken = default);
}
