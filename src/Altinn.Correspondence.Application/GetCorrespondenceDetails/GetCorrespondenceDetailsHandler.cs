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
    ILogger<GetCorrespondenceDetailsHandler> logger) : IHandler<GetCorrespondenceDetailsRequest, GetCorrespondenceDetailsResponse>
{
    public async Task<OneOf<GetCorrespondenceDetailsResponse, Error>> Process(GetCorrespondenceDetailsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken);
        if (correspondence == null)
        {
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
            return AuthorizationErrors.NoAccessToResource;
        }
        var latestStatus = correspondence.GetHighestStatusWithoutPurged();
        if (latestStatus == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        return await TransactionWithRetriesPolicy.Execute<GetCorrespondenceDetailsResponse>(async (cancellationToken) =>
        {
            if (hasAccessAsRecipient && !user.CallingAsSender())
            {
                if (!latestStatus.Status.IsAvailableForRecipient())
                {
                    return CorrespondenceErrors.CorrespondenceNotFound;
                }
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Fetched,
                    StatusText = CorrespondenceStatus.Fetched.ToString(),
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);

            }

            
            var notificationStatuses = new List<NotificationStatusResponse>();
            foreach (var notification in correspondence.Notifications)
            {
                // If the notification does not have a shipmentId, it is a version 1 notification
                if (notification.ShipmentId == null && notification.NotificationOrderId != null)
                {
                    var notificationStatus = await altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);
                    notificationStatus.IsReminder = notification.IsReminder;
                    notificationStatuses.Add(notificationStatus);
                }
                // If the notification has a shipmentId, it is a version 2 notification
                else if (notification.ShipmentId is not null)
                {
                    var notificationDetails = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.ToString(), cancellationToken);
                    notificationStatuses.Add(MapNotificationV2ToV1(notificationDetails, correspondence, notification));
                }
            }

            var response = new GetCorrespondenceDetailsResponse
            {
                CorrespondenceId = correspondence.Id,
                Status = latestStatus.Status,
                StatusText = latestStatus.StatusText,
                StatusChanged = latestStatus.StatusChanged,
                SendersReference = correspondence.SendersReference,
                Sender = correspondence.Sender,
                MessageSender = correspondence.MessageSender ?? string.Empty,
                Created = correspondence.Created,
                Recipient = correspondence.Recipient,
                Content = correspondence.Content!,
                ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
                Notifications = notificationStatuses,
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
            };
            return response;
        }, logger, cancellationToken);
    }

    private static NotificationStatusResponse MapNotificationV2ToV1(NotificationStatusResponseV2 notificationDetails, CorrespondenceEntity correspondence, CorrespondenceNotificationEntity notification)
    {
        var latestEmailRecipient = notificationDetails.Recipients
            .Where(r => r.Type == "Email")
            .OrderByDescending(r => r.LastUpdate)
            .FirstOrDefault();

        var latestSmsRecipient = notificationDetails.Recipients
            .Where(r => r.Type == "SMS")
            .OrderByDescending(r => r.LastUpdate)
            .FirstOrDefault();

        var emailRecipients = notificationDetails.Recipients
            .Where(r => r.Type == "Email")
            .OrderByDescending(r => r.LastUpdate)
            .ToList();

        var smsRecipients = notificationDetails.Recipients
            .Where(r => r.Type == "SMS")
            .OrderByDescending(r => r.LastUpdate)
            .ToList();

        return new NotificationStatusResponse
        {
            Id = notificationDetails.ShipmentId.ToString(),
            SendersReference = notificationDetails.SendersReference,
            RequestedSendTime = notification.RequestedSendTime.DateTime,
            Created = notification.Created.DateTime,
            Creator = "To be changed",
            IsReminder = notification.IsReminder,
            NotificationChannel = notification.NotificationChannel,
            ResourceId = correspondence.ResourceId,
            IgnoreReservation = correspondence.IgnoreReservation ?? false,
            ProcessingStatus = new StatusExt
            {
                Status = notificationDetails.Status,
                LastUpdate = notificationDetails.LastUpdate.DateTime
            },
            NotificationsStatusDetails = new NotificationsStatusDetails
            {
                Email = latestEmailRecipient != null ? new EmailNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        EmailAddress = latestEmailRecipient?.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = latestEmailRecipient?.Status ?? string.Empty,
                        LastUpdate = latestEmailRecipient?.LastUpdate.DateTime ?? DateTime.MinValue
                    },
                    Succeeded = latestEmailRecipient?.Status == "Email_Delivered"
                } : null,
                Sms = latestSmsRecipient != null ? new SmsNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        MobileNumber = latestSmsRecipient?.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = latestSmsRecipient?.Status ?? string.Empty,
                        LastUpdate = latestSmsRecipient?.LastUpdate.DateTime ?? DateTime.MinValue
                    },
                    Succeeded = latestSmsRecipient?.Status == "SMS_Delivered"
                } : null,
                Emails = emailRecipients!= null && emailRecipients.Count != 0 ? [.. emailRecipients.Select(r => new EmailNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        EmailAddress = r.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = r.Status,
                        LastUpdate = r.LastUpdate.DateTime
                    },
                    Succeeded = r.Status == "Email_Delivered"
                })]: null,
                Smses = smsRecipients!= null && smsRecipients.Count != 0 ? [.. smsRecipients.Select(r => new SmsNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        MobileNumber = r.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = r.Status,
                        LastUpdate = r.LastUpdate.DateTime
                    },
                    Succeeded = r.Status == "SMS_Delivered"
                })]: null,
            }
        };
    }
}
