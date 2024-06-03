using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatusCommand;

public class UpdateCorrespondenceStatusCommandHandler : IHandler<UpdateCorrespondenceStatusCommandRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    public UpdateCorrespondenceStatusCommandHandler(ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository)
    {
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
    }

    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusCommandRequest request, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var currentStatus = await _correspondenceStatusRepository.GetLatestStatusByCorrespondenceId(request.CorrespondenceId, cancellationToken);
        if ((request.Status == CorrespondenceStatus.Confirmed || request.Status == CorrespondenceStatus.Read) && currentStatus?.Status != CorrespondenceStatus.Published)
        {
            return Errors.CorrespondenceNotPublished;
        }
        if (currentStatus?.Status == request.Status)
        {
            return request.CorrespondenceId;
        }

        await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
        {
            CorrespondenceId = request.CorrespondenceId,
            Status = request.Status,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = request.Status.ToString(),
        }, cancellationToken);
        return request.CorrespondenceId;
    }
}
