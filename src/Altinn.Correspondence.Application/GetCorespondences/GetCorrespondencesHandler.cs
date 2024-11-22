using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    UserClaimsHelper userClaimsHelper) : IHandler<GetCorrespondencesRequest, GetCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;

    public async Task<OneOf<GetCorrespondencesResponse, Error>> Process(GetCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (request.Limit < 0 || request.Offset < 0)
        {
            return Errors.OffsetAndLimitIsNegative;
        }
        var limit = request.Limit == 0 ? 50 : request.Limit;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;
        if (from != null && to != null && from > to)
        {
            return Errors.InvalidDateRange;
        }

        string? onBehalfOf = request.OnBehalfOf;
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(
            user,
            request.ResourceId,
            [ResourceAccessLevel.Read, ResourceAccessLevel.Write],
            cancellationToken,
            onBehalfOf: string.IsNullOrEmpty(onBehalfOf) ? null : onBehalfOf);

        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        string? orgNo = _userClaimsHelper.GetUserID();
        if (hasAccess && !string.IsNullOrEmpty(onBehalfOf))
        {
            orgNo = onBehalfOf;
        }

        if (orgNo is null)
        {
            return Errors.CouldNotFindOrgNo;
        }
        var correspondences = await _correspondenceRepository.GetCorrespondences(
            request.ResourceId,
            request.Offset,
            limit,
            from,
            to,
            request.Status,
            orgNo,
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
