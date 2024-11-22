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
    ICorrespondenceRepository CorrespondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    UserClaimsHelper userClaimsHelper,
    ILogger<GetCorrespondenceOverviewHandler> logger) : IHandler<GetCorrespondenceOverviewRequest, GetCorrespondenceOverviewResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly ICorrespondenceRepository _CorrespondenceRepository = CorrespondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;
    private readonly ILogger<GetCorrespondenceOverviewHandler> _logger = logger;

    public async Task<OneOf<GetCorrespondenceOverviewResponse, Error>> Process(GetCorrespondenceOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await _CorrespondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, cancellationToken);
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
            _logger.LogWarning("Caller not affiliated with correspondence");
            return Errors.CorrespondenceNotFound;
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            _logger.LogWarning("Latest status not found for correspondence");
            return Errors.CorrespondenceNotFound;
        }

        if (isRecipient)
        {
            if (!latestStatus.Status.IsAvailableForRecipient())
            {
                _logger.LogWarning("Rejected because correspondence not available for recipient in current state.");
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
    }
}
