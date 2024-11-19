using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewHandler : IHandler<Guid, GetCorrespondenceOverviewResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _CorrespondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly ILogger<GetCorrespondenceOverviewHandler> _logger;

    public GetCorrespondenceOverviewHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository CorrespondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, UserClaimsHelper userClaimsHelper, ILogger<GetCorrespondenceOverviewHandler> logger)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _CorrespondenceRepository = CorrespondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _userClaimsHelper = userClaimsHelper;
        _logger = logger;
    }

    public async Task<OneOf<GetCorrespondenceOverviewResponse, Error>> Process(Guid CorrespondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await _CorrespondenceRepository.GetCorrespondenceById(CorrespondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        if(!_userClaimsHelper.IsPersonallyAffiliatedWithCorrespondence(correspondence.Recipient, correspondence.Sender))
        {
            var hasResourceAccess = await _altinnAuthorizationService.CheckUserAccess(user, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read, ResourceAccessLevel.Write }, cancellationToken);
            if (!hasResourceAccess)
            {
                return Errors.NoAccessToResource;
            }
            if (!_userClaimsHelper.IsAffiliatedWithCorrespondence(correspondence.Recipient, correspondence.Sender))
            {
                _logger.LogWarning("Caller not affiliated with correspondence");
                return Errors.CorrespondenceNotFound;
            }
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            _logger.LogWarning("Latest status not found for correspondence");
            return Errors.CorrespondenceNotFound;
        }

        if (_userClaimsHelper.IsRecipient(correspondence.Recipient))
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
