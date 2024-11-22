using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public class GetCorrespondenceDetailsHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnNotificationService altinnNotificationService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    UserClaimsHelper userClaimsHelper,
    ILogger<GetCorrespondenceDetailsHandler> logger) : IHandler<GetCorrespondenceDetailsRequest, GetCorrespondenceDetailsResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAltinnNotificationService _altinnNotificationService = altinnNotificationService;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;
    private readonly ILogger<GetCorrespondenceDetailsHandler> _logger = logger;

    public async Task<OneOf<GetCorrespondenceDetailsResponse, Error>> Process(GetCorrespondenceDetailsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        string? onBehalfOf = request.OnBehalfOf;
        bool isOnBehalfOfRecipient = false;
        bool isOnBehalfOfSender = false;

        if (!string.IsNullOrEmpty(onBehalfOf))
        {
            isOnBehalfOfRecipient = correspondence.Recipient.GetOrgNumberWithoutPrefix() == onBehalfOf.GetOrgNumberWithoutPrefix();
            isOnBehalfOfSender = correspondence.Sender.GetOrgNumberWithoutPrefix() == onBehalfOf.GetOrgNumberWithoutPrefix();
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            [ResourceAccessLevel.Read, ResourceAccessLevel.Write],
            cancellationToken,
            isOnBehalfOfRecipient || isOnBehalfOfSender ? onBehalfOf : null,
            isOnBehalfOfRecipient || isOnBehalfOfSender ? correspondence?.Id.ToString() : null);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        bool isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient) || isOnBehalfOfRecipient;
        bool isSender = _userClaimsHelper.IsSender(correspondence.Sender) || isOnBehalfOfSender;

        if (!isRecipient && !isSender)
        {
            return Errors.CorrespondenceNotFound;
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        return await TransactionWithRetriesPolicy.Execute<GetCorrespondenceDetailsResponse>(async (cancellationToken) =>
        {
            if (isRecipient)
            {
                if (!latestStatus.Status.IsAvailableForRecipient())
                {
                    return Errors.CorrespondenceNotFound;
                }
                await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Fetched,
                    StatusText = CorrespondenceStatus.Fetched.ToString(),
                    StatusChanged = DateTimeOffset.UtcNow
                }, cancellationToken);

            }
            var notificationHistory = new List<NotificationStatusResponse>();
            foreach (var notification in correspondence.Notifications)
            {
                if (notification.NotificationOrderId != null)
                {
                    var notificationSummary = await _altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString());
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
                StatusHistory = correspondence.Statuses?.OrderBy(s => s.StatusChanged).ToList() ?? new List<CorrespondenceStatusEntity>(),
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
        }, _logger, cancellationToken);
    }
}
