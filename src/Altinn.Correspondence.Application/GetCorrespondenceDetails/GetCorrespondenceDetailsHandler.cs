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
    IResourceRegistryService resourceRegistryService,
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
        var latestStatus = correspondence.GetHighestStatus();
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

            
            var notificationStatus = new List<NotificationStatusResponse>();
            foreach (var notification in correspondence.Notifications)
            {
                // If the notification does not have a shipmentId, it is a version 1 notification
                if (notification.ShipmentId == null && notification.NotificationOrderId != null)
                {
                    var notificationDetails = await altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);
                    notificationDetails.IsReminder = notification.IsReminder;
                    notificationStatus.Add(notificationDetails);
                }
                // If the notification has a shipmentId, it is a version 2 notification
                else if (notification.ShipmentId is not null)
                {
                    var notificationDetails = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.ToString(), cancellationToken);
                    notificationStatus.Add(await MapNotificationV2ToV1Async(notificationDetails, notification));
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
            };
            return response;
        }, logger, cancellationToken);
    }

    private async Task<NotificationStatusResponse> MapNotificationV2ToV1Async(NotificationStatusResponseV2 notificationDetails, CorrespondenceNotificationEntity notification)
    {
        var correspondence = notification.Correspondence ?? throw new ArgumentException($"Correspondence with id {notification.CorrespondenceId} not found when mapping notification", nameof(notification));
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
            Creator = correspondence?.ResourceId != null ? await resourceRegistryService.GetServiceOwnerOrgCode(correspondence.ResourceId) : "Not found",
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
                    Succeeded = latestEmailRecipient?.Status == "Email_Succeeded"
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
                    Succeeded = latestSmsRecipient?.Status == "SMS_Succeeded"
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
                    Succeeded = r.Status == "Email_Succeeded"
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
                    Succeeded = r.Status == "SMS_Succeeded"
                })]: null,
            }
        };
    }
}
