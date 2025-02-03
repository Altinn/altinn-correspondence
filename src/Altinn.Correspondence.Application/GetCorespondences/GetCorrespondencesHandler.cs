using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using OneOf;
using Serilog.Context;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    IHttpContextAccessor httpContextAccessor) : IHandler<GetCorrespondencesRequest, GetCorrespondencesResponse>
{
    public async Task<OneOf<GetCorrespondencesResponse, Error>> Process(GetCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        LogContext.PushProperty("resourceId", request.ResourceId);
        const int limit = 1000;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;
        if (from != null && to != null && from > to)
        {
            return CorrespondenceErrors.InvalidDateRange;
        }
        string? onBehalfOf = request.OnBehalfOf?.WithoutPrefix() ?? httpContextAccessor.HttpContext?.User.GetCallerOrganizationId();
        if (onBehalfOf is null)
        {
            return AuthorizationErrors.CouldNotDetermineCaller;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsAny(
            user,
            request.ResourceId,
            onBehalfOf,
            cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        // TODO: Add implementation to retrieve instances delegated to the user

        var correspondences = await correspondenceRepository.GetCorrespondences(
            request.ResourceId,
            limit,
            from,
            to,
            request.Status,
            onBehalfOf,
            request.Role,
            cancellationToken);
        var response = new GetCorrespondencesResponse
        {
            Ids = correspondences.Item1,
        };
        return response;
    }
}
