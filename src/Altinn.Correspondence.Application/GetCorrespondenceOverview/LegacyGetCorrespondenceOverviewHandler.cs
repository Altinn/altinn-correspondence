using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewHandler : IHandler<LegacyGetCorrespondenceOverviewRequest, LegacyGetCorrespondenceOverviewResponse>
{
    private readonly IAltinnAccessManagementService _altinnAccessManagementService;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly ILogger<LegacyGetCorrespondenceOverviewHandler> _logger;

    public LegacyGetCorrespondenceOverviewHandler(IAltinnAccessManagementService altinnAccessManagementService, IAltinnAuthorizationService altinnAuthorizationService, IAltinnRegisterService altinnRegisterService, ICorrespondenceRepository CorrespondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, UserClaimsHelper userClaimsHelper, ILogger<LegacyGetCorrespondenceOverviewHandler> logger)
    {
        _altinnAccessManagementService = altinnAccessManagementService;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnRegisterService = altinnRegisterService;
        _correspondenceRepository = CorrespondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _logger = logger;
    }

    public async Task<OneOf<LegacyGetCorrespondenceOverviewResponse, Error>> Process(LegacyGetCorrespondenceOverviewRequest request, CancellationToken cancellationToken)
    {
        if (request.PartyId == 0 || request.PartyId == int.MinValue)
        {
            return Errors.InvalidPartyId;
        }

        var userParty = await _altinnRegisterService.LookUpPartyByPartyId(request.PartyId, cancellationToken);
        if (userParty == null || (string.IsNullOrEmpty(userParty.SSN) && string.IsNullOrEmpty(userParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return Errors.LegacyNoAccessToCorrespondence;
        }
        var recipients = new List<string>();
        if (correspondence.Recipient != userParty.SSN && correspondence.Recipient != ("0192:" + userParty.OrgNumber))
        {
            var authorizedParties = await _altinnAccessManagementService.GetAuthorizedParties(userParty, cancellationToken);
            var isAuthorized = authorizedParties.Any(p => ("0192:" + p.OrgNumber) == correspondence.Recipient || p.SSN == correspondence.Recipient);
            if (!isAuthorized)
            {
                return Errors.LegacyNoAccessToCorrespondence;
            }
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            _logger.LogWarning("Latest status not found for correspondence");
            return Errors.CorrespondenceNotFound;
        }
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            _logger.LogWarning("Rejected because correspondence not available for recipient in current state.");
            return Errors.CorrespondenceNotFound;
        }
        try
        {
            await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Fetched,
                StatusText = CorrespondenceStatus.Fetched.ToString(),
                StatusChanged = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error when adding status to correspondence");
        }

        var notificationsOverview = new List<CorrespondenceNotificationOverview>();
        if (correspondence.Notifications != null)
        {
            foreach (var notification in correspondence.Notifications)
            {
                notificationsOverview.Add(new CorrespondenceNotificationOverview
                {
                    NotificationOrderId = notification.NotificationOrderId,
                    IsReminder = notification.IsReminder
                });
            }
        }
        var resourceOwnerParty = await _altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (resourceOwnerParty == null)
        {
            return Errors.CouldNotFindOrgNo;
        }
        var response = new LegacyGetCorrespondenceOverviewResponse
        {
            CorrespondenceId = correspondence.Id,
            Attachments = correspondence.Content.Attachments ?? new List<CorrespondenceAttachmentEntity>(),
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText,
            StatusChanged = latestStatus.StatusChanged,
            ResourceId = correspondence.ResourceId,
            Sender = correspondence.Sender,
            SendersReference = correspondence.SendersReference,
            MessageSender = String.IsNullOrWhiteSpace(correspondence.MessageSender) ? resourceOwnerParty.Name : correspondence.MessageSender,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
            Notifications = notificationsOverview,
            ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
            RequestedPublishTime = correspondence.RequestedPublishTime,
            IgnoreReservation = correspondence.IgnoreReservation ?? false,
            MarkedUnread = correspondence.MarkedUnread,
            AllowSystemDeleteAfter = correspondence.AllowSystemDeleteAfter,
            Published = correspondence.Published,
            IsConfirmationNeeded = correspondence.IsConfirmationNeeded,
            MinimumAuthenticationLevel = (int)minimumAuthLevel,
            AuthorizedForSign = true,
            DueDateTime = correspondence.DueDateTime,
            AllowDelete = true,
            Archived = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Archived)?.StatusChanged,
            Confirmed = correspondence.Statuses?.FirstOrDefault(s => s.Status == CorrespondenceStatus.Confirmed)?.StatusChanged,
            PropertyList = correspondence.PropertyList ?? new Dictionary<string, string>(),
            InstanceOwnerPartyId = resourceOwnerParty.PartyId
        };
        return response;
    }
}
