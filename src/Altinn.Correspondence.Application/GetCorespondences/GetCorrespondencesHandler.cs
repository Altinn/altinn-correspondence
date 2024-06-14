using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class GetCorrespondencesHandler : IHandler<GetCorrespondencesRequest, GetCorrespondencesResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;

    public GetCorrespondencesHandler(ICorrespondenceRepository correspondenceRepository)
    {
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<GetCorrespondencesResponse, Error>> Process(GetCorrespondencesRequest request, CancellationToken cancellationToken)
    {
        if (request.limit < 0 || request.offset < 0)
        {
            return Errors.OffsetAndLimitIsNegative;
        }

        var limit = request.limit == 0 ? 50 : request.limit;
        var correspondences = await _correspondenceRepository.GetCorrespondences(request.offset, limit, request.from, request.to, request.status, cancellationToken);

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
