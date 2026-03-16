using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;

using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.SendNotificationOrder;

namespace Altinn.Correspondence.Application.UnreadConfidentialCorrespondence;

public class UnreadConfidentialCorrespondenceHandler(
    ILogger<UnreadConfidentialCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    IConfidentialReminderRepository confidentialReminderRepository,
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
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Read))
        {
            return;
        }

        logger.LogInformation("Correspondence with id {correspondenceId} has not been read, processing unread confidential correspondence", correspondenceId);

        var reminder = new ConfidentialReminderDialogDto
        {
            Id = Guid.CreateVersion7(),
            Title = "Din virksomhet har en uåpnet taushetsbelagt post",
            Summary = "Din virksomhet har mottatt ett eller flere brev som er taushetsbelagte og som ikke er åpnet. Dette varselet inneholder informasjon om hvordan du kan lese disse",
            Recipient = correspondence.Recipient.WithUrnPrefix(),
            ResourceId = "correspondence-attachment-test",
            SendersReference = "Digdir",
            MessageSender = "Digitaliseringsdirektoratet",
            Created = DateTimeOffset.UtcNow,
            Status = "RequiresAttention",
            PropertyList = new Dictionary<string, string>{}
        };


        var notificationRequest = new NotificationRequest
        {
            NotificationTemplate = NotificationTemplate.CustomMessage,
            EmailSubject = $"Din virksomhet $recipientName$ har uåpnet taushetsbelagt post." ,
            EmailBody = "Dette er et automatisk varsel om at din virksomhet har mottatt taushetsbelagt post som ikke er åpnet. \n\n Logg inn i Altinn for å se hvilke meldinger det gjelder og hvordan du kan åpne dem.",
            NotificationChannel = NotificationChannel.Email,
            SendReminder = false,
            EmailContentType = EmailContentType.Plain
        };
        logger.LogInformation(
            "Enqueueing notification for confidential reminder {ReminderId}, recipient {Recipient}, channel {Channel}, template {Template}",
            reminder.Id, reminder.Recipient, notificationRequest.NotificationChannel, notificationRequest.NotificationTemplate);

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

        if (await confidentialReminderRepository.NumberOfRemindersForRecipient(reminder.Recipient, cancellationToken) > 0)
        {
            logger.LogInformation("Existing confidential reminder dialog found for recipient {Recipient}, skipping dialog creation and notification", reminder.Recipient);
            var existingDialogId = await confidentialReminderRepository.GetDialogIdOfReminderForRecipient(reminder.Recipient, cancellationToken);
            if (existingDialogId.HasValue)
            {
                logger.LogInformation("Linking correspondence {correspondenceId} to existing dialog {DialogId}", correspondenceId, existingDialogId.Value);
                await confidentialReminderRepository.AddConfidentialReminder(new ConfidentialReminderEntity
                {
                    Id = reminder.Id,
                    CorrespondenceId = correspondenceId,
                    Recipient = reminder.Recipient.WithUrnPrefix(),
                    DialogId = existingDialogId.Value
                }, cancellationToken);
                return;
            }
            logger.LogWarning("Existing reminders found for recipient {Recipient} but no dialog ID could be retrieved, proceeding with new dialog creation", reminder.Recipient);
        }
        logger.LogInformation("Creating confidential reminder dialog for correspondence with id {correspondenceId}", correspondenceId);

        var dialogId = await dialogportenService.CreateConfidentialReminderDialog(reminder);
        if (string.IsNullOrEmpty(dialogId))
        {
            logger.LogError("Failed to create confidential reminder dialog for correspondence with id {correspondenceId}", correspondenceId);
            return;
        }

        await confidentialReminderRepository.AddConfidentialReminder(new ConfidentialReminderEntity
        {
            Id = reminder.Id,
            CorrespondenceId = correspondenceId,
            Recipient = reminder.Recipient.WithUrnPrefix(),
            DialogId = Guid.Parse(dialogId),
        }, cancellationToken);
        logger.LogInformation("Confidential reminder {ReminderId} persisted for correspondence {correspondenceId}", reminder.Id, correspondenceId);
        return;
    }
}
