using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    UserClaimsHelper userClaimsHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<LegacyGetCorrespondenceOverviewHandler> logger) : IHandler<Guid, LegacyGetCorrespondenceOverviewResponse>
{
    public async Task<OneOf<LegacyGetCorrespondenceOverviewResponse, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var operationTimestamp = DateTimeOffset.UtcNow;

        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var userParty = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (userParty == null || (string.IsNullOrEmpty(userParty.SSN) && string.IsNullOrEmpty(userParty.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken, true);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, userParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return AuthorizationErrors.LegacyNoAccessToCorrespondence;
        }
        var latestStatus = correspondence.GetHighestStatusForLegacyCorrespondence();
        if (latestStatus == null)
        {
            logger.LogWarning("Latest status not found for correspondence");
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (!latestStatus.Status.IsAvailableForLegacyRecipient())
        {
            logger.LogWarning("Rejected because correspondence not available for recipient in current state.");
            return CorrespondenceErrors.CorrespondenceNotFound;
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
        var resourceOwnerParty = await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (resourceOwnerParty == null)
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        if (userParty.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        return await TransactionWithRetriesPolicy.Execute<LegacyGetCorrespondenceOverviewResponse>(async (cancellationToken) =>
        {
            try
            {
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Fetched,
                    StatusText = CorrespondenceStatus.Fetched.ToString(),
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when adding status to correspondence");
            }

            try
            {
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Read,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Read.ToString(),
                    PartyUuid = partyUuid
                }, cancellationToken);
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateOpenedActivity(correspondence.Id, DialogportenActorType.Recipient, operationTimestamp));
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when adding status to correspondence");
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
        }, logger, cancellationToken);
    }
}
