using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
public class UpdateCorrespondenceStatusHelper
{
    private readonly IDialogportenService _dialogportenService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    public UpdateCorrespondenceStatusHelper(IDialogportenService dialogportenService, IBackgroundJobClient backgroundJobClient)
    {
        _dialogportenService = dialogportenService;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Validates if the current status of the correspondence allows for status updates.
    /// </summary>
    /// <param name="correspondence">The correspondence entity to validate</param>
    /// <returns></returns>
    public Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        var currentStatus = correspondence.GetHighestStatus();
        if (currentStatus is null)
        {
            return Errors.LatestStatusIsNull;
        }
        if (!currentStatus.Status.IsAvailableForRecipient())
        {
            return Errors.CorrespondenceNotFound;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return Errors.CorrespondencePurged;
        }
        return null;
    }
    /// <summary>
    /// Validates if the requested status update is allowed based on the current correspondence state.
    /// </summary>
    /// <param name="request">The status update request to validate.</param>
    /// <param name="correspondence">The correspondence entity to update.</param>
    /// <returns>An Error if validation fails, null if successful.</returns>
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

    /// <summary>
    /// Publishes appropriate events based on the correspondence status update.
    /// </summary>
    /// <param name="eventBus">The event bus service.</param>
    /// <param name="correspondence">The correspondence entity being updated.</param>
    /// <param name="status">The new status being set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
    /// <summary>
    /// Reports activity to Dialogporten based on the correspondence status update.
    /// </summary>
    /// <param name="correspondenceId"></param>
    /// <param name="status"></param>
    // Must be public to be run by Hangfire
    public void ReportActivityToDialogporten(Guid correspondenceId, CorrespondenceStatus status)
    {
        if (status == CorrespondenceStatus.Confirmed)
        {
            _backgroundJobClient.Enqueue(() => _dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.Recipient, DialogportenTextType.CorrespondenceConfirmed));
        }
        else if (status == CorrespondenceStatus.Archived)
        {
            _backgroundJobClient.Enqueue(() => _dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.Recipient, DialogportenTextType.CorrespondenceArchived));
        }
        return;
    }
}