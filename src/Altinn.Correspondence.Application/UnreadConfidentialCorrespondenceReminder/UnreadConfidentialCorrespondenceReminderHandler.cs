using System.Security.Claims;
using System.Text.Json;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;
using Altinn.Correspondence.Application.InitializeCorrespondences;

using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.UnreadConfidentialCorrespondence;

public class UnreadConfidentialCorrespondenceHandler(
    ILogger<UnreadConfidentialCorrespondenceHandler> logger,
    ICorrespondenceRepository correspondenceRepository,
    IConfidentialReminderRepository confidentialReminderRepository,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    InitializeCorrespondencesHandler initializeCorrespondencesHandler,
    HttpClient client)
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
            Summary = $"Dere har mottatt en melding fra {correspondence.Sender.WithoutPrefix()}. For å se denne meldingen kreves tilgang til {correspondence.ResourceId}. Hovedadministrator må delegere denne tilgangen for at noen skal kunne se denne meldingen. Se mer informasjon på våre hjelpesider: https://info.altinn.no/nyheter/tilgang-til-taushetsbelagt-post/",
            Recipient = correspondence.Recipient,
            ResourceId = correspondence.ResourceId,
            SendersReference = "Digdir",
            Sender = "Digdir",
            Created = DateTimeOffset.UtcNow,
            PropertyList = new Dictionary<string, string>
            {
                {"originalSender", correspondence.Sender.WithoutPrefix()}
            }
        };

        if (await confidentialReminderRepository.RecipientHasConfidentialReminder(reminder.Recipient, cancellationToken))
        {
            logger.LogInformation("Recipient {recipient} already has a confidential reminder, skipping creation of new dialog", reminder.Recipient);
            var existingDialogId = await confidentialReminderRepository.GetDialogIdOfReminderForRecipient(reminder.Recipient, cancellationToken);
            if (!string.IsNullOrEmpty(existingDialogId)){
                logger.LogInformation("Existing confidential reminder dialog found with id {dialogId} for recipient {recipient}", existingDialogId, reminder.Recipient);
                await confidentialReminderRepository.AddConfidentialReminder(new ConfidentialReminderEntity
                {
                    Id = reminder.Id,
                    CorrespondenceId = correspondenceId,
                    Recipient = reminder.Recipient,
                    DialogId = Guid.Parse(existingDialogId)
                }, cancellationToken);
                return;
            }
        }

        logger.LogInformation("Creating confidential reminder dialog for correspondence with id {correspondenceId}", correspondenceId);

        var senderOrgNumber = correspondence.Sender.WithoutPrefix();
        var consumerJson = JsonSerializer.Serialize(new TokenConsumer
        {
            Authority = "iso6523-actorid-upis",
            ID = $"0192:{senderOrgNumber}"
        });
        var internalUser = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("scope", "altinn:serviceowner altinn:correspondence.write"),
                new Claim("consumer", consumerJson),
            ],
            authenticationType: "internal",
            nameType: null,
            roleType: null
        ));

        var dialogId = await dialogportenService.CreateConfidentialReminderDialog(reminder);
        await confidentialReminderRepository.AddConfidentialReminder(new ConfidentialReminderEntity
        {
            Id = reminder.Id,
            CorrespondenceId = correspondenceId,
            Recipient = reminder.Recipient,
            DialogId = Guid.Parse(dialogId),
        }, cancellationToken);
    }
}
