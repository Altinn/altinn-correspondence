using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.SendNotificationOrder;
using Altinn.Correspondence.Persistence.Helpers;

namespace Altinn.Correspondence.Application.UnreadConfidentialCorrespondence;

public class UnreadConfidentialCorrespondenceHandler(
    ILogger<UnreadConfidentialCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    IConfidentialReminderRepository confidentialReminderRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task Process(Guid correspondenceId, CancellationToken cancellationToken = default)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found when processing unread confidential correspondence", correspondenceId);
            return;
        }
        var latestStatus = correspondence.GetHighestStatus();
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Read) || !latestStatus.Status.IsAvailableForRecipient())
        {
            return;
        }

        logger.LogInformation("Correspondence with id {correspondenceId} has not been read, processing unread confidential correspondence", correspondenceId);

        var recipient = correspondence.Recipient.WithUrnPrefix();
        var reminder = new ConfidentialReminderDialogDto
        {
            Id = Guid.CreateVersion7(),
            Title = "", // Value for title and summary is assigned in the mapper based on the users language
            Summary = "",
            Recipient = recipient,
            ResourceId = "digdir-reminder-unopened-confidential-correspondences",
            SendersReference = "corr-confidential-reminder",
            Sender = "991825827",
            Created = DateTimeOffset.UtcNow,
            Status = "RequiresAttention",
            PropertyList = new Dictionary<string, string>{}
        };

        Guid? dialogId = null;

        var existingDialogId = await confidentialReminderRepository.GetDialogIdOfReminderForRecipient(recipient, cancellationToken);
        if (existingDialogId.HasValue)
        {
            logger.LogInformation("Reusing existing dialog {DialogId}", existingDialogId.Value);
            dialogId = existingDialogId.Value;
        }
        else
        {
            // Pre-allocate a UUID v7 dialog id in IdempotencyKeys so concurrent jobs share one Dialogporten create.
            dialogId = await GetOrCreateDialogIdempotencyKey(recipient, cancellationToken);
            reminder.DialogId = dialogId.Value;

            logger.LogInformation("Creating confidential reminder dialog for correspondence with id {correspondenceId}", correspondenceId);
            try
            {
                var createdDialogId = await dialogportenService.CreateConfidentialReminderDialog(reminder);
                dialogId = Guid.TryParse(createdDialogId, out var parsedDialogId) ? parsedDialogId : dialogId;
                logger.LogInformation("Confidential reminder dialog created with id {DialogId} for correspondence {correspondenceId}", dialogId, correspondenceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create confidential reminder dialog for correspondence {correspondenceId} — persisting reminder without dialog ID", correspondenceId);
                dialogId = null;
            }
        }
        
        await confidentialReminderRepository.AddConfidentialReminder(new ConfidentialReminderEntity
        {
            Id = reminder.Id,
            CorrespondenceId = correspondenceId,
            Recipient = recipient,
            DialogId = dialogId,
        }, cancellationToken);
        logger.LogInformation("Confidential reminder {ReminderId} persisted for correspondence {correspondenceId} with dialog {DialogId}", reminder.Id, correspondenceId, dialogId?.ToString() ?? "none");

        var notificationRequest = new NotificationRequest
        {
            NotificationTemplate = NotificationTemplate.CustomMessage,
            EmailSubject = $"Din virksomhet $recipientName$ har uåpnet taushetsbelagt post.",
            EmailBody = "Dette er et automatisk varsel om at din virksomhet har mottatt taushetsbelagt post som ikke er åpnet. \n\n Logg inn i Altinn for å se hvilke meldinger det gjelder og hvordan du kan åpne dem.",
            NotificationChannel = NotificationChannel.Email,
            SendReminder = false,
            EmailContentType = EmailContentType.Plain
        };

        var notificationJobId = backgroundJobClient.Enqueue<CreateNotificationOrderHandler>((handler) => handler.Process(new CreateNotificationOrderForConfidentialReminders()
        {
            Reminder = reminder,
            CorrespondenceId = correspondenceId,
            NotificationRequest = notificationRequest,
            Language = "nb",
        }, cancellationToken));
        logger.LogInformation("Notification job enqueued with id {NotificationJobId} for confidential reminder {ReminderId}", notificationJobId, reminder.Id);

        backgroundJobClient.ContinueJobWith<SendNotificationOrderHandler>(notificationJobId, (handler) => handler.Process(correspondenceId, CancellationToken.None));
        logger.LogInformation("Send notification job scheduled as continuation of {NotificationJobId} for correspondence {CorrespondenceId}", notificationJobId, correspondenceId);
    }

    private async Task<Guid> GetOrCreateDialogIdempotencyKey(string recipientUrn, CancellationToken cancellationToken)
    {
        var existing = await idempotencyKeyRepository.GetByPartyUrnAndTypeAsync(
            recipientUrn,
            IdempotencyType.ConfidentialReminderDialog,
            cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Reusing confidential reminder dialog idempotency key {DialogId} for recipient {Recipient}", existing.Id, recipientUrn);
            return existing.Id;
        }

        var dialogId = Guid.CreateVersion7();
        try
        {
            await idempotencyKeyRepository.CreateAsync(new IdempotencyKeyEntity
            {
                Id = dialogId,
                CorrespondenceId = null,
                PartyUrn = recipientUrn,
                StatusAction = null,
                IdempotencyType = IdempotencyType.ConfidentialReminderDialog
            }, cancellationToken);
            logger.LogInformation("Created confidential reminder dialog idempotency key {DialogId} for recipient {Recipient}", dialogId, recipientUrn);
            return dialogId;
        }
        catch (DbUpdateException e) when (e.IsPostgresUniqueViolation())
        {
            var raced = await idempotencyKeyRepository.GetByPartyUrnAndTypeAsync(
                recipientUrn,
                IdempotencyType.ConfidentialReminderDialog,
                cancellationToken);
            if (raced is null)
            {
                throw;
            }
            logger.LogInformation("Concurrent create of confidential reminder dialog key; using existing {DialogId} for recipient {Recipient}", raced.Id, recipientUrn);
            return raced.Id;
        }
    }
}
