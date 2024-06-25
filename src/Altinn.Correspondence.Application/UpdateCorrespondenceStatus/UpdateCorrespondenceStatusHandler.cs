using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;

public class UpdateCorrespondenceStatusHandler : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IEventBus _eventBus;

    public UpdateCorrespondenceStatusHandler(ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IEventBus eventBus)
    {
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
    }

    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var currentStatus = correspondence.Statuses.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if ((request.Status == CorrespondenceStatus.Confirmed || request.Status == CorrespondenceStatus.Read) && currentStatus?.Status < CorrespondenceStatus.Published)
        {
            return Errors.CorrespondenceNotPublished;
        }
        if (currentStatus?.Status == CorrespondenceStatus.PurgedByRecipient || currentStatus?.Status == CorrespondenceStatus.PurgedByAltinn)
        {
            return Errors.CorrespondencePurged;
        }
        if (currentStatus?.Status >= request.Status)
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
