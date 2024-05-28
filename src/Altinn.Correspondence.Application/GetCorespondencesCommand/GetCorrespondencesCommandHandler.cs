using Altinn.Correspondence.Application.GetCorrespondencesResponse;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondencesCommand;

public class GetCorrespondencesCommandHandler : IHandler<GetCorrespondencesCommandRequest, GetCorrespondencesCommandResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    public GetCorrespondencesCommandHandler(ICorrespondenceRepository correspondenceRepository, IAttachmentRepository attachmentRepository)
    {
        _correspondenceRepository = correspondenceRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<GetCorrespondencesCommandResponse, Error>> Process(GetCorrespondencesCommandRequest request, CancellationToken cancellationToken)
    {
        if (request.limit < 0 || request.offset < 0)
        {
            return Errors.OffsetAndLimitIsNegative;
        }

        var limit = request.limit == 0 ? 50 : request.limit;
        var correspondences = await _correspondenceRepository.GetCorrespondences(request.offset, limit, request.from, request.to, request.status, cancellationToken);

        var response = new GetCorrespondencesCommandResponse
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
