using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
public class UpdateCorrespondenceStatusHelper
{
    public Error? ValidateUpdateRequest(UpdateCorrespondenceStatusRequest request, CorrespondenceEntity correspondence)
    {
        if (request.Status == CorrespondenceStatus.Read && !correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return Errors.ReadBeforeFetched;
        }
        if (request.Status == CorrespondenceStatus.Confirmed && !correspondence.StatusHasBeen(CorrespondenceStatus.Fetched))
        {
            return Errors.ConfirmBeforeFetched;
        }
        if (request.Status == CorrespondenceStatus.Archived && correspondence.IsConfirmationNeeded is true && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            return Errors.ArchiveBeforeConfirmed;
        }
        return null;
    }
    public async Task PublishEvent(IEventBus eventBus, CorrespondenceEntity correspondence, CorrespondenceStatus status, CancellationToken cancellationToken)
    {
        if (status == CorrespondenceStatus.Confirmed)
        {
            await eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        }
        else if (status == CorrespondenceStatus.Read)
        {
            await eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        }
    }
}