using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GetCorrespondencesHandler> logger) : IHandler<GetCorrespondencesRequest, GetCorrespondencesResponse>
{
    public async Task<OneOf<GetCorrespondencesResponse, Error>> Process(GetCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Searching for correspondences of {ResourceId}", request.ResourceId.SanitizeForLogging());
        const int limit = 1000;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;
        if (from != null && to != null && from > to)
        {
            logger.LogWarning("Invalid date range provided - from {From} is after to {To}", from, to);
            return CorrespondenceErrors.InvalidDateRange;
        }
        string? onBehalfOf = request.OnBehalfOf?.WithoutPrefix() ?? httpContextAccessor.HttpContext?.User.GetCallerOrganizationId();
        if (onBehalfOf is null)
        {
            logger.LogError("Could not determine caller organization ID");
            return AuthorizationErrors.CouldNotDetermineCaller;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsAny(
            user,
            request.ResourceId,
            onBehalfOf,
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for resource {ResourceId} on behalf of {OnBehalfOf}", request.ResourceId.SanitizeForLogging(), onBehalfOf.SanitizeForLogging());
            return AuthorizationErrors.NoAccessToResource;
        }
        // TODO: Add implementation to retrieve instances delegated to the user

        logger.LogInformation("Retrieving correspondences for resource {ResourceId} with filters: from={From}, to={To}, limit={Limit} status={Status}, onBehalfOf={onBehalfOf}, role={Role}",
            request.ResourceId.SanitizeForLogging(),
            from,
            to,
            limit,
            request.Status,
            onBehalfOf.SanitizeForLogging(),
            request.Role
        );
        var correspondenceIds = await correspondenceRepository.GetCorrespondences(
            request.ResourceId,
            limit,
            from,
            to,
            request.Status,
            onBehalfOf,
            request.Role,
            request.SendersReference,
            cancellationToken);
        logger.LogInformation("Found {Count} correspondences for resource {ResourceId}", correspondenceIds.Count, request.ResourceId.SanitizeForLogging());
        return new GetCorrespondencesResponse { Ids = correspondenceIds };
    }
}
