using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public class GetCorrespondenceDetailsHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnNotificationService altinnNotificationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    NotificationMapper notificationMapper,
    ILogger<GetCorrespondenceDetailsHandler> logger) : IHandler<GetCorrespondenceDetailsRequest, GetCorrespondenceDetailsResponse>
{
    public async Task<OneOf<GetCorrespondenceDetailsResponse, Error>> Process(GetCorrespondenceDetailsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing correspondence details request for ID {CorrespondenceId}", request.CorrespondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var hasAccessAsRecipient = await altinnAuthorizationService.CheckAccessAsRecipient(
            user,
            correspondence,
            cancellationToken);
        var hasAccessAsSender = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            correspondence,
            cancellationToken);
        if (!hasAccessAsRecipient && !hasAccessAsSender)
        {
            logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user has neither recipient nor sender access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }
        logger.LogInformation("User has {AccessType} access to correspondence {CorrespondenceId}",
            hasAccessAsRecipient ? "recipient" : "sender",
            request.CorrespondenceId);
        var latestStatus = correspondence.GetHighestStatus();
        if (latestStatus == null)
        {
            logger.LogWarning("No status found for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        return await TransactionWithRetriesPolicy.Execute<GetCorrespondenceDetailsResponse>(async (cancellationToken) =>
        {
            if (hasAccessAsRecipient && !user.CallingAsSender())
            {
                if (!latestStatus.Status.IsAvailableForRecipient())
                {
                    logger.LogWarning("Correspondence {CorrespondenceId} status {Status} is not available for recipient",
                        request.CorrespondenceId,
                        latestStatus.Status);
                    return CorrespondenceErrors.CorrespondenceNotFound;
                }
                logger.LogInformation("Adding Fetched status for correspondence {CorrespondenceId} by recipient {PartyUuid}",
                    request.CorrespondenceId,
                    partyUuid);
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Fetched,
                    StatusText = CorrespondenceStatus.Fetched.ToString(),
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);
            }
            var notificationStatus = new List<NotificationStatusResponse>();
            foreach (var notification in correspondence.Notifications)
            {
                // If the notification does not have a shipmentId, it is a version 1 notification
                if (notification.ShipmentId == null && notification.NotificationOrderId != null)
                {
                    logger.LogInformation("Processing v1 notification {NotificationOrderId} for correspondence {CorrespondenceId}",
                        notification.NotificationOrderId,
                        request.CorrespondenceId);
                    var notificationDetails = await altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);
                    notificationDetails.IsReminder = notification.IsReminder;
                    notificationStatus.Add(notificationDetails);
                }
                // If notification does not have a shipmentId, and also has no NotificationOrderId, it's probably an Altinn2Notification.
                else if (notification.ShipmentId == null && notification.Altinn2NotificationId != null)
                {
                    notificationStatus.Add(await notificationMapper.MapAltinn2NotificationToAltinn3NotificationStatus(notification));
                }
                // If the notification has a shipmentId, it is a version 2 notification
                else if (notification.ShipmentId is not null)
                {
                    logger.LogInformation("Processing v2 notification {ShipmentId} for correspondence {CorrespondenceId}",
                        notification.ShipmentId,
                        request.CorrespondenceId);
                    var notificationDetails = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.ToString(), cancellationToken);
                    notificationStatus.Add(await notificationMapper.MapNotificationV2ToV1Async(notificationDetails, notification));
                }
            }

            logger.LogInformation("Successfully retrieved details for correspondence {CorrespondenceId} with status {Status}",
                request.CorrespondenceId,
                latestStatus.Status);
            return new GetCorrespondenceDetailsResponse
            {
                CorrespondenceId = correspondence.Id,
                Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                Status = latestStatus.Status,
                StatusText = latestStatus.StatusText,
                StatusChanged = latestStatus.StatusChanged,
                SendersReference = correspondence.SendersReference,
                Sender = correspondence.Sender,
                MessageSender = correspondence.MessageSender ?? string.Empty,
                Created = correspondence.Created,
                Recipient = correspondence.Recipient,
                Content = hasAccessAsRecipient || !correspondence.StatusHasBeen(CorrespondenceStatus.Published) ? correspondence.Content : null,
                ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
                Notifications = notificationStatus,
                StatusHistory = correspondence.Statuses
                    .Where(statusEntity => hasAccessAsRecipient ? true : statusEntity.Status.IsAvailableForSender())
                    .OrderBy(s => s.StatusChanged)
                    .ToList(),
                ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
                ResourceId = correspondence.ResourceId,
                RequestedPublishTime = correspondence.RequestedPublishTime,
                IgnoreReservation = correspondence.IgnoreReservation ?? false,
                AllowSystemDeleteAfter = correspondence.AllowSystemDeleteAfter,
                DueDateTime = correspondence.DueDateTime,
                PropertyList = correspondence.PropertyList,
                Published = correspondence.Published,
                IsConfirmationNeeded = correspondence.IsConfirmationNeeded,
                IsConfidential = correspondence.IsConfidential
            };
        }, logger, cancellationToken);
    }
}
