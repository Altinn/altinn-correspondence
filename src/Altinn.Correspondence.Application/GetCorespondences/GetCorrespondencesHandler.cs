using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    IHttpContextAccessor httpContextAccessor) : IHandler<GetCorrespondencesRequest, GetCorrespondencesResponse>
{
    public async Task<OneOf<GetCorrespondencesResponse, Error>> Process(GetCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (request.Limit < 0 || request.Offset < 0)
        {
            return CorrespondenceErrors.OffsetAndLimitIsNegative;
        }
        var limit = request.Limit == 0 ? 50 : request.Limit;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;
        if (from != null && to != null && from > to)
        {
            return CorrespondenceErrors.InvalidDateRange;
        }
        string? onBehalfOf = request.OnBehalfOf;
        if (onBehalfOf is null) { 
            onBehalfOf = "0192:" + httpContextAccessor.HttpContext?.User.GetCallerOrganizationId();
        }
        if (onBehalfOf is null)
        {
            return AuthorizationErrors.CouldNotDetermineCaller;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsAny(
            user,
            request.ResourceId,
            onBehalfOf.WithoutPrefix(),
            cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        // TODO: Add implementation to retrieve instances delegated to the user

        var correspondences = await correspondenceRepository.GetCorrespondences(
            request.ResourceId,
            request.Offset,
            limit,
            from,
            to,
            request.Status,
            onBehalfOf,
            request.Role,
            cancellationToken);
        var response = new GetCorrespondencesResponse
        {
            Items = correspondences.Item1,
            Pagination = new PaginationMetaData
            {
                Offset = request.Offset,
                Limit = limit,
                TotalItems = correspondences.Item2
            }
        };
        return response;
    }
}
