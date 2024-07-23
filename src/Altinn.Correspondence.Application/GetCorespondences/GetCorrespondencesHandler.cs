using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesHandler : IHandler<GetCorrespondencesRequest, GetCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;

    public GetCorrespondencesHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository correspondenceRepository)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<GetCorrespondencesResponse, Error>> Process(GetCorrespondencesRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.resourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (request.limit < 0 || request.offset < 0)
        {
            return Errors.OffsetAndLimitIsNegative;
        }

        var limit = request.limit == 0 ? 50 : request.limit;
        DateTimeOffset? to = request.to != null ? ((DateTimeOffset)request.to).ToUniversalTime() : null;
        DateTimeOffset? from = request.from != null ? ((DateTimeOffset)request.from).ToUniversalTime() : null;
        var correspondences = await _correspondenceRepository.GetCorrespondences(request.offset, limit, from, to, request.status, cancellationToken);

        var response = new GetCorrespondencesResponse
        {
            Items = correspondences.Item1,
            Pagination = new PaginationMetaData
            {
                Offset = request.offset,
                Limit = limit,
                TotalItems = correspondences.Item2
            }
        };
        return response;
    }
}