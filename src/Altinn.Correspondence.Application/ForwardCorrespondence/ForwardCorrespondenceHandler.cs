using System.Security.Claims;
using Altinn.Correspondence.Application.CheckForwardedCorrespondenceDelivery;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class ForwardCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnNotificationService altinnNotificationService,
    ICorrespondenceRepository correspondenceRepository,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceForwardingEventRepository correspondenceForwardingEventRepository,
    ComposedEmailHelper composedEmailHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<ForwardCorrespondenceHandler> logger
) : IHandler<ForwardCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(ForwardCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            logger.LogError("Forwarding operation attempted without authenticated user context");
            return AuthorizationErrors.NoAccessToResource;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken, true);

        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var userHasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken);
        if (!userHasAccess)
        {
            logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }
        
        if (!correspondence.AllowForwarding)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} does not allow forwarding", request.CorrespondenceId);
            return CorrespondenceErrors.ForwardingNotAllowed;
        }
        if (request.ForwardingText is not null)
        {
            if (request.ForwardingText.Length > 200)
            {
                logger.LogWarning("Forwarding text for correspondence {CorrespondenceId} exceeds 200 characters", request.CorrespondenceId);
                return CorrespondenceErrors.ForwardingTextTooLong;
            }
            if (!TextValidation.ValidatePlainText(request.ForwardingText))
            {
                logger.LogWarning("Forwarding text for correspondence {CorrespondenceId} is not plain text", request.CorrespondenceId);
                return CorrespondenceErrors.ForwardingTextIsNotPlainText;
            }
        }

        var hasBeenRead = correspondence.StatusHasBeen(CorrespondenceStatus.Read);
        if (!hasBeenRead)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} has not been read and cannot be forwarded", request.CorrespondenceId);
            return CorrespondenceErrors.ForwardBeforeRead;
        }
        var alreadyForwardedToRecipient = await correspondenceForwardingEventRepository.HasCorrespondenceBeenForwardedToRecipient(request.CorrespondenceId, request.ForwardTo, cancellationToken);
        if (alreadyForwardedToRecipient)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} has already been forwarded to {ForwardTo}", request.CorrespondenceId, request.ForwardTo.SanitizeForLogging());
            return CorrespondenceErrors.CorrespondenceAlreadyForwardedToRecipient;
        }

        var caller = user.GetCallerPartyUrn();
        var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
        if (party?.Uuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for caller {Caller}", caller.SanitizeForLogging());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        var forwardingEvent = new CorrespondenceForwardingEventEntity
        {
            Id = Guid.NewGuid(),
            ForwardedOnDate = DateTimeOffset.UtcNow,
            ForwardedByPartyUuid = partyUuid,
            ForwardedByUserUuid = partyUuid,
            ForwardedToEmailAddress = request.ForwardTo,
            ForwardingText = request.ForwardingText,
            CorrespondenceId = request.CorrespondenceId,
            Correspondence = correspondence
        };
        var forwardingEventId = await correspondenceForwardingEventRepository.AddForwardingEventForSync(forwardingEvent, cancellationToken);
        if (forwardingEventId == Guid.Empty)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} has already been forwarded to {ForwardTo}", request.CorrespondenceId, request.ForwardTo.SanitizeForLogging());
            return CorrespondenceErrors.CorrespondenceAlreadyForwardedToRecipient;
        }

        logger.LogInformation("Forwarding correspondence {CorrespondenceId} to {ForwardTo}", request.CorrespondenceId, request.ForwardTo.SanitizeForLogging());
        try
        {
            var composedEmailRequest = await composedEmailHelper.MapToComposedEmailRequest(correspondence, request.ForwardTo, request.ForwardingText, cancellationToken);
            var composedEmailResponse = await altinnNotificationService.CreateComposedEmail(composedEmailRequest, cancellationToken);
            if (composedEmailResponse == null)
            {
                logger.LogError("Failed to create composed email for correspondence {CorrespondenceId}", request.CorrespondenceId);
                await correspondenceForwardingEventRepository.DeleteForwardingEvent(forwardingEventId, cancellationToken);
                return CorrespondenceErrors.ForwardingNotAllowed;
            }

            await correspondenceForwardingEventRepository.SetNotificationShipmentId(forwardingEventId, composedEmailResponse.Notification.ShipmentId, cancellationToken);

            backgroundJobClient.Enqueue<CheckForwardedCorrespondenceDeliveryHandler>(x =>
                x.Process(composedEmailResponse.Notification.ShipmentId, forwardingEventId, CancellationToken.None));
            return forwardingEventId;
        }
        catch
        {
            await correspondenceForwardingEventRepository.DeleteForwardingEvent(forwardingEventId, cancellationToken);
            throw;
        }
    }
}