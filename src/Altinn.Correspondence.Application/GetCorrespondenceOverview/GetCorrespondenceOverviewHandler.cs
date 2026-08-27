using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Hangfire;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    IConfidentialReminderRepository confidentialReminderRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    IHybridCacheWrapper cache,
    PublishCorrespondenceHandler publishCorrespondenceHandler,
    ILogger<GetCorrespondenceOverviewHandler> logger,
    ApplicationDbContext dbContext) : IHandler<GetCorrespondenceOverviewRequest, GetCorrespondenceOverviewResponse>
{
    public async Task<OneOf<GetCorrespondenceOverviewResponse, Error>> Process(GetCorrespondenceOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing correspondence overview request for {CorrespondenceId}", request.CorrespondenceId);

        var operationTimestamp = DateTimeOffset.UtcNow;
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, includeStatus: true, includeContent: true, includeForwardingEvents: false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
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
            logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient or sender access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }

        var didAttemptInlinePublish = await AttemptImmediatePublishIfDelayed(correspondence, hasAccessAsRecipient, user, operationTimestamp, cancellationToken);
        if (didAttemptInlinePublish)
        {
            correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, includeStatus: true, includeContent: true, includeForwardingEvents: false, cancellationToken) ?? correspondence;
            operationTimestamp = DateTimeOffset.UtcNow;
        }
        
        var latestStatus = correspondence.GetHighestStatus();

        if (correspondence.GetPurgedStatus() != null)
        {
            logger.LogWarning("Access denied - correspondence has been purged");
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var party = await altinnRegisterService.LookUpPartyById(user?.GetCallerPartyUrn() ?? string.Empty, cancellationToken);
        if (party?.Uuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        return await DatabaseTransactionHelper.ExecuteAsync<OneOf<GetCorrespondenceOverviewResponse, Error>>(dbContext, async (cancellationToken) =>
        {
            DateTimeOffset? readTimestamp = null;
            if (hasAccessAsRecipient && !user.CallingAsSender())
            {
                if (!latestStatus.Status.IsAvailableForRecipient())
                {
                    logger.LogWarning("Rejected because correspondence not available for recipient in current state.");
                    return CorrespondenceErrors.CorrespondenceNotFound;
                }
                var cacheKey = $"Correspondence_Fetched_Debounce:{correspondence.Id}-{partyUuid}";
                await cache.GetOrCreateAsync(
                    cacheKey,
                    async cancellationToken =>
                    {
                        await correspondenceStatusRepository.AddCorrespondenceStatusFetched(new CorrespondenceStatusFetchedEntity
                        {
                            CorrespondenceId = correspondence.Id,
                            Status = CorrespondenceStatus.Fetched,
                            StatusText = CorrespondenceStatus.Fetched.ToString(),
                            StatusChanged = operationTimestamp,
                            PartyUuid = partyUuid
                        }, cancellationToken);
                        return true;
                    },
                    new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(15) },
                    null,
                    cancellationToken);
                if (request.OnlyGettingContent)
                {
                    if (!correspondence.StatusHasBeen(CorrespondenceStatus.Read)) {
                        await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                        {
                            CorrespondenceId = correspondence.Id,
                            Status = CorrespondenceStatus.Read,
                            StatusText = CorrespondenceStatus.Read.ToString(),
                            StatusChanged = operationTimestamp,
                            PartyUuid = partyUuid
                        }, cancellationToken);
                        readTimestamp = operationTimestamp;
                        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(
                            AltinnEventType.CorrespondenceReceiverRead,
                            correspondence.ResourceId,
                            correspondence.Id.ToString(),
                            "correspondence",
                            correspondence.Sender,
                            CancellationToken.None));
                        var callerPartyUrn = user?.GetCallerPartyUrn() ?? string.Empty;
                        backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateOpenedActivity(correspondence.Id, DialogportenActorType.Recipient, operationTimestamp, callerPartyUrn));
                    }
                }
                if (correspondence.IsConfidential
                    && hasAccessAsRecipient
                    && !(user?.CallingAsSender() ?? false)
                    && await confidentialReminderRepository.CorrespondenceHasReminder(correspondence.Id, cancellationToken))
                {
                    var recipient = correspondence.Recipient.WithUrnPrefix();
                    await PostgresAdvisoryLock.AcquireTransactionLockAsync(
                        dbContext,
                        $"confidential-reminder-dialog:{recipient}",
                        cancellationToken);

                    // Re-read under lock: another cleanup path may have already removed this reminder.
                    var targetReminder = await confidentialReminderRepository.GetByCorrespondenceId(
                        correspondence.Id,
                        cancellationToken);
                    if (targetReminder is null)
                    {
                        logger.LogInformation(
                            "Confidential reminder for correspondence {CorrespondenceId} was already removed; skipping cleanup",
                            correspondence.Id);
                    }
                    else
                    {
                        var isFinalReminderForRecipient =
                            await confidentialReminderRepository.NumberOfRemindersForRecipient(recipient, cancellationToken) == 1;
                        var reminderDialogId = isFinalReminderForRecipient ? targetReminder.DialogId : null;

                        await confidentialReminderRepository.RemoveConfidentialReminderByCorrespondenceId(correspondence.Id, cancellationToken);

                        if (isFinalReminderForRecipient)
                        {
                            // Release idempotency key before enqueueing soft-delete so a later create allocates a new dialog id.
                            await idempotencyKeyRepository.DeleteByPartyUrnAndTypeAsync(
                                recipient,
                                IdempotencyType.ConfidentialReminderDialog,
                                cancellationToken);
                            if (reminderDialogId.HasValue)
                            {
                                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.TrySoftDeleteDialog(reminderDialogId.Value.ToString()));
                            }
                        }
                    }
                }
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

            CorrespondenceContentEntity? content = null;
            if (hasAccessAsRecipient || !correspondence.StatusHasBeen(CorrespondenceStatus.Published))
            {
                content = correspondence.Content;
                if (content != null && correspondence.ReplyOptions?.Count > 3)
                {
                    var replyLabel = content.Language?.ToLower() switch
                    {
                        "en" => "Reply options:",
                        "nn" => "Svarval:",
                        _ => "Svarvalg:"
                    };
                    content = new CorrespondenceContentEntity
                    {
                        Language = content.Language ?? "nb",
                        MessageTitle = content.MessageTitle,
                        MessageSummary = content.MessageSummary,
                        MessageBody = content.MessageBody + "\n\n" + replyLabel + "\n" +
                            string.Join("\n", correspondence.ReplyOptions.Select(ro => $"{ro.LinkText}: {ro.LinkURL}")),
                        Attachments = content.Attachments
                    };
                }
            }

            var response = new GetCorrespondenceOverviewResponse
            {
                CorrespondenceId = correspondence.Id,
                Content = content,
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
                PropertyList = correspondence.PropertyList ?? new Dictionary<string, string>(),
                ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
                RequestedPublishTime = correspondence.RequestedPublishTime,
                IgnoreReservation = correspondence.IgnoreReservation ?? false,
                Published = correspondence.Published,
                Read = readTimestamp ?? correspondence.GetReadTimestamp(),
                IsConfirmationNeeded = correspondence.IsConfirmationNeeded,
                IsConfidential = correspondence.IsConfidential,
                Altinn2CorrespondenceId = correspondence.Altinn2CorrespondenceId,
                AllowForwarding = correspondence.AllowForwarding,
                DueDateTime = correspondence.DueDateTime
            };
            logger.LogInformation("Successfully retrieved overview for correspondence {CorrespondenceId} with status {Status}", 
                request.CorrespondenceId, 
                latestStatus.Status);
            return response;
        }, cancellationToken);
    }

    private async Task<bool> AttemptImmediatePublishIfDelayed(
        CorrespondenceEntity correspondence,
        bool hasAccessAsRecipient,
        ClaimsPrincipal? user,
        DateTimeOffset operationTimestamp,
        CancellationToken cancellationToken)
    {
        var isRecipientFetch = hasAccessAsRecipient && !(user?.CallingAsSender() ?? false);
        var isPastRequestedPublishTime = correspondence.RequestedPublishTime <= operationTimestamp;
        var isAwaitingPublish = !correspondence.StatusHasBeen(CorrespondenceStatus.Published)
            && !correspondence.StatusHasBeen(CorrespondenceStatus.Failed);
        if (!isRecipientFetch || !isPastRequestedPublishTime || !isAwaitingPublish)
        {
            return false;
        }

        var hasDialogReference = correspondence.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId);
        if (!hasDialogReference)
        {
            return false;
        }

        logger.LogInformation("Publish for correspondence {CorrespondenceId} appears delayed; running inline before serving recipient", correspondence.Id);
        await cache.GetOrCreateAsync(
            $"InlinePublish:{correspondence.Id}",
            async ct =>
            {
                await publishCorrespondenceHandler.Process(correspondence.Id, null, ct);
                return true;
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(30) },
            null,
            cancellationToken);

        return true;
    }
}
