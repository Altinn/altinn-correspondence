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
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (request.Limit < 0 || request.Offset < 0)
        {
            return Errors.OffsetAndLimitIsNegative;
        }

        var limit = request.Limit == 0 ? 50 : request.Limit;
        DateTimeOffset? to = request.To != null ? ((DateTimeOffset)request.To).ToUniversalTime() : null;
        DateTimeOffset? from = request.From != null ? ((DateTimeOffset)request.From).ToUniversalTime() : null;
        var correspondences = await _correspondenceRepository.GetCorrespondences(request.ResourceId, request.Offset, limit, from, to, request.Status, cancellationToken);

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