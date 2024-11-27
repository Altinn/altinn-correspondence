using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    UserClaimsHelper userClaimsHelper,
    ILogger<GetCorrespondenceOverviewHandler> logger) : IHandler<GetCorrespondenceOverviewRequest, GetCorrespondenceOverviewResponse>
{
    public async Task<OneOf<GetCorrespondenceOverviewResponse, Error>> Process(GetCorrespondenceOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
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
            return Errors.NoAccessToResource;
        }
        var latestStatus = correspondence.GetHighestStatus();
        if (latestStatus == null)
        {
            logger.LogWarning("Latest status not found for correspondence");
            return Errors.CorrespondenceNotFound;
        }

        return await TransactionWithRetriesPolicy.Execute<GetCorrespondenceOverviewResponse>(async (cancellationToken) =>
        {
            if (!hasAccessAsSender && hasAccessAsRecipient)
            {
                if (!latestStatus.Status.IsAvailableForRecipient())
                {
                    logger.LogWarning("Rejected because correspondence not available for recipient in current state.");
                    return Errors.CorrespondenceNotFound;
                }
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Fetched,
                    StatusText = CorrespondenceStatus.Fetched.ToString(),
                    StatusChanged = DateTimeOffset.UtcNow
                }, cancellationToken);

            }
            var notificationsOverview = new List<CorrespondenceNotificationOverview>();
            foreach (var notification in correspondence.Notifications)
            {
                notificationsOverview.Add(new CorrespondenceNotificationOverview
                {
                    NotificationOrderId = notification.NotificationOrderId,
                    IsReminder = notification.IsReminder
                });
            }

            var response = new GetCorrespondenceOverviewResponse
            {
                CorrespondenceId = correspondence.Id,
                Content = correspondence.Content,
                Status = latestStatus.Status,
                StatusText = latestStatus.StatusText,
                StatusChanged = latestStatus.StatusChanged,
                ResourceId = correspondence.ResourceId,
                Sender = correspondence.Sender,
                SendersReference = correspondence.SendersReference,
                MessageSender = correspondence.MessageSender ?? string.Empty,
                Created = correspondence.Created,
                Recipient = correspondence.Recipient,
                ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
                Notifications = notificationsOverview,
                ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
                RequestedPublishTime = correspondence.RequestedPublishTime,
                IgnoreReservation = correspondence.IgnoreReservation ?? false,
                AllowSystemDeleteAfter = correspondence.AllowSystemDeleteAfter,
                Published = correspondence.Published,
                IsConfirmationNeeded = correspondence.IsConfirmationNeeded,
            };
            return response;
        }, logger, cancellationToken);
    }
}
