using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Persistence;
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
    IConfidentialReminderRepository confidentialReminderRepository,
    IDialogportenService dialogportenService,
    UserClaimsHelper userClaimsHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<LegacyGetCorrespondenceOverviewHandler> logger,
    ApplicationDbContext dbContext) : IHandler<Guid, LegacyGetCorrespondenceOverviewResponse>
{
    public async Task<OneOf<LegacyGetCorrespondenceOverviewResponse, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var operationTimestamp = DateTimeOffset.UtcNow;

        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var userParty = await altinnRegisterService.LookUpPartyById(partyId.ToString(), cancellationToken);
        if (userParty == null || string.IsNullOrEmpty(userParty.GetExternalUrn()))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken, true);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(
            user,
            userParty.GetUserId()?.ToString() ?? userClaimsHelper.GetUserId().ToString(),
            correspondence.ResourceId,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            correspondence.Recipient,
            cancellationToken);
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
        if (userParty?.Uuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        return await DatabaseTransactionHelper.ExecuteAsync(dbContext, async (cancellationToken) =>
        {
            try
            {
                await correspondenceStatusRepository.AddCorrespondenceStatusFetched(new CorrespondenceStatusFetchedEntity
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
                MessageSender = String.IsNullOrWhiteSpace(correspondence.MessageSender) ? resourceOwnerParty.GetDisplayName() : correspondence.MessageSender,
                Created = correspondence.Created,
                Recipient = correspondence.Recipient,
                ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
                Notifications = notificationsOverview,
                ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
                RequestedPublishTime = correspondence.RequestedPublishTime,
                IgnoreReservation = correspondence.IgnoreReservation ?? false,
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
                InstanceOwnerPartyId = resourceOwnerParty.GetPartyId()
            };

            try
            {
                if (correspondence.IsConfidential 
                    && await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken)
                    && !(user?.CallingAsSender() ?? false) 
                    && await confidentialReminderRepository.CorrespondenceHasReminder(correspondence.Id, cancellationToken))
                {
                    if (await confidentialReminderRepository.NumberOfRemindersForRecipient(correspondence.Recipient, cancellationToken) == 1)
                    {
                        var reminderDialogId = await confidentialReminderRepository.GetDialogIdOfReminderForRecipient(correspondence.Recipient, cancellationToken);
                        if (reminderDialogId.HasValue)
                        {
                            await dialogportenService.TrySoftDeleteDialog(reminderDialogId.Value.ToString());
                        }
                    }
                    await confidentialReminderRepository.RemoveConfidentialReminderByCorrespondenceId(correspondence.Id, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to clean up confidential reminder for correspondence {CorrespondenceId}", correspondence.Id);
            }
            return response;
        }, cancellationToken);
    }
}
