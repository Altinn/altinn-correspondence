using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateMarkUnread;

public class UpdateMarkUnreadHandler : IHandler<Guid, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IEventBus _eventBus;

    public UpdateMarkUnreadHandler(ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IEventBus eventBus)
    {
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
    }

    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var currentStatus = correspondence.Statuses.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if (!correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Read))
        {
            return Errors.CorrespondenceHasNotBeenRead;
        }
        if (currentStatus?.Status == CorrespondenceStatus.PurgedByRecipient || currentStatus?.Status == CorrespondenceStatus.PurgedByAltinn)
        {
            return Errors.CorrespondencePurged;
        }

        await _correspondenceRepository.UpdateMarkedUnread(correspondenceId, true, cancellationToken);
        return correspondenceId;
    }
}
