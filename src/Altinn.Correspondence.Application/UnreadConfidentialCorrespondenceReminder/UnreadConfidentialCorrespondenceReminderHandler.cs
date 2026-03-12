using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;

using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.UnreadConfidentialCorrespondence;

public class UnreadConfidentialCorrespondenceHandler(
    ILogger<UnreadConfidentialCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    IConfidentialReminderRepository confidentialReminderRepository,
    IDialogportenService dialogportenService)
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
            Recipient = correspondence.Recipient,
            ResourceId = "correspondence-attachment-test",
            SendersReference = "Digdir",
            Sender = "Digdir",
            Created = DateTimeOffset.UtcNow,
            Status = "RequiresAttention",
            PropertyList = new Dictionary<string, string>{}
        };

        if (await confidentialReminderRepository.NumberOfRemindersForRecipient(reminder.Recipient, cancellationToken) > 0)
        {
            logger.LogInformation("Recipient {recipient} already has a confidential reminder, skipping creation of new dialog", reminder.Recipient);
            var existingDialogId = await confidentialReminderRepository.GetDialogIdOfReminderForRecipient(reminder.Recipient, cancellationToken);
            if (existingDialogId.HasValue)
            {
                logger.LogInformation("Existing confidential reminder dialog found with id {dialogId} for recipient {recipient}", existingDialogId.Value, reminder.Recipient);
                await confidentialReminderRepository.AddConfidentialReminder(new ConfidentialReminderEntity
                {
                    Id = reminder.Id,
                    CorrespondenceId = correspondenceId,
                    Recipient = reminder.Recipient,
                    DialogId = existingDialogId.Value
                }, cancellationToken);
                return;
            }
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
            Recipient = reminder.Recipient,
            DialogId = Guid.Parse(dialogId),
        }, cancellationToken);
        return;
    }
}
