using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewHandler : IHandler<Guid, LegacyGetCorrespondenceOverviewResponse>
{
    private readonly IAltinnAccessManagementService _altinnAccessManagementService;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly ILogger<LegacyGetCorrespondenceOverviewHandler> _logger;
    private readonly UserClaimsHelper _userClaimsHelper;


    public LegacyGetCorrespondenceOverviewHandler(IAltinnAccessManagementService altinnAccessManagementService, IAltinnAuthorizationService altinnAuthorizationService, IAltinnRegisterService altinnRegisterService, ICorrespondenceRepository CorrespondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, UserClaimsHelper userClaimsHelper, ILogger<LegacyGetCorrespondenceOverviewHandler> logger)
    {
        _altinnAccessManagementService = altinnAccessManagementService;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnRegisterService = altinnRegisterService;
        _correspondenceRepository = CorrespondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _logger = logger;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<LegacyGetCorrespondenceOverviewResponse, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        if (_userClaimsHelper.GetPartyId() is not int partyId)
        {
            return Errors.InvalidPartyId;
        }
        var userParty = await _altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (userParty == null || (string.IsNullOrEmpty(userParty.SSN) && string.IsNullOrEmpty(userParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(userParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return Errors.LegacyNoAccessToCorrespondence;
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
            Attachments = correspondence.Content!.Attachments ?? new List<CorrespondenceAttachmentEntity>(),
            Language = correspondence.Content!.Language,
            MessageTitle = correspondence.Content!.MessageTitle,
            MessageSummary = TextValidation.ConvertToHtml(correspondence.Content!.MessageSummary),
            MessageBody = TextValidation.ConvertToHtml(correspondence.Content!.MessageBody),
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
            AllowSystemDeleteAfter = correspondence.AllowSystemDeleteAfter,
            Published = correspondence.Published,
            IsConfirmationNeeded = correspondence.IsConfirmationNeeded,
            MinimumAuthenticationLevel = (int)minimumAuthLevel,
            AuthorizedForWrite = true,
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
