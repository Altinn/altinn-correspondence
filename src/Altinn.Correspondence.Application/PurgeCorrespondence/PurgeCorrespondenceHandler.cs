using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IEventBus eventBus,
    UserClaimsHelper userClaimsHelper,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    ILogger<PurgeCorrespondenceHandler> logger) : IHandler<PurgeCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(PurgeCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        Guid correspondenceId = request.CorrespondenceId;
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var hasAccessAsSender = await altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Sender.WithoutPrefix(),
            correspondence.Id.ToString(),
            [ResourceAccessLevel.Read, ResourceAccessLevel.Write],
            cancellationToken);
        var hasAccessAsRecipient = await altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Recipient.WithoutPrefix(),
            correspondence.Id.ToString(),
            [ResourceAccessLevel.Read, ResourceAccessLevel.Write],
            cancellationToken);
        if (!hasAccessAsSender && !hasAccessAsRecipient)
        {
            return Errors.NoAccessToResource;
        }
        if (hasAccessAsSender)
        {
            var senderRecipientPurgeError = purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderRecipientPurgeError is not null)
            {
                return senderRecipientPurgeError;
            }
        }
        else if (hasAccessAsRecipient)
        {
            var recipientPurgeError = purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientPurgeError is not null)
            {
                return recipientPurgeError;
            }
        }
        var currentStatusError = purgeCorrespondenceHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var status = hasAccessAsSender ? CorrespondenceStatus.PurgedByAltinn : CorrespondenceStatus.PurgedByRecipient;
            await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = status.ToString()
            }, cancellationToken);

            await eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            await purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondenceId, cancellationToken);
            purgeCorrespondenceHelper.ReportActivityToDialogporten(hasAccessAsSender, correspondenceId);
            purgeCorrespondenceHelper.CancelNotification(correspondenceId, cancellationToken);
            return correspondenceId;
        }, logger, cancellationToken);
    }
}