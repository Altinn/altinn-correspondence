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
            var notificationHistory = new List<NotificationStatusResponse>();
            foreach (var notification in correspondence.Notifications)
            {
                if (notification.NotificationOrderId != null)
                {
                    var notificationSummary = await altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);
                    notificationSummary.IsReminder = notification.IsReminder;
                    notificationHistory.Add(notificationSummary);
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
                Notifications = notificationHistory,
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
            return response;
        }, logger, cancellationToken);
    }
}
