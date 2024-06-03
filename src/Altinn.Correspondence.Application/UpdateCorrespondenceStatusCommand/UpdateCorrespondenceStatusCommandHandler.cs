using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatusCommand;

public class UpdateCorrespondenceStatusCommandHandler : IHandler<UpdateCorrespondenceStatusCommandRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IEventBus _eventBus;
    public UpdateCorrespondenceStatusCommandHandler(ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IEventBus eventBus)
    {
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
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
        await PublishEvent(request.CorrespondenceId, request.Status, cancellationToken);
        return request.CorrespondenceId;
    }

    private async Task PublishEvent(Guid correspondenceId, CorrespondenceStatus status, CancellationToken cancellationToken)
    {
        if (status == CorrespondenceStatus.Confirmed)
        {
            await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, null, correspondenceId.ToString(), "correspondence", null, cancellationToken);
        }
        else if (status == CorrespondenceStatus.Read)
        {
            await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, null, correspondenceId.ToString(), "correspondence", null, cancellationToken);
        }
    }
}
