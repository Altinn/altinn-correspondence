using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers;

public class ComposedEmailHelper(
    IStorageRepository storageRepository,
    IAltinnRegisterService altinnRegisterService,
    ILogger<ComposedEmailHelper> logger
)
{
    public async Task<ComposedEmailRequest> MapToComposedEmailRequest(CorrespondenceEntity correspondence, string forwardTo, Guid forwardedByPartyUuid, string? forwardingText, bool includeAttachments, CancellationToken cancellationToken)
    {
        var performedBy = await PerformedByResolver.ResolveAsync(
            altinnRegisterService,
            [forwardedByPartyUuid],
            correspondence.Sender,
            logger,
            cancellationToken);
        var forwardedBy = performedBy.GetValueOrDefault(forwardedByPartyUuid)?.ActorName;
        var sender = correspondence.MessageSender
            ?? await altinnRegisterService.LookupNameByOrganizationNumber(correspondence.Sender, cancellationToken)
            ?? correspondence.Sender;

        return new ComposedEmailRequest
        {
            IdempotencyId = correspondence.Id.CreateVersion5($"ForwardCorrespondence:{forwardTo}"),
            SendersReference = correspondence.SendersReference,
            RequestedSendTime = DateTimeOffset.UtcNow.AddSeconds(10),
            Recipient = new ComposedEmailRecipient
            {
                EmailAddress = forwardTo,
                EmailSettings = new ComposedEmailRecipientSettings
                {
                    Subject = ComposeSubject(correspondence),
                    Body = ComposeBody(correspondence, sender, forwardedBy, forwardingText),
                    ContentType = "Plain",
                    Attachments = includeAttachments ? await GetComposedEmailAttachments(correspondence, cancellationToken) : new List<ComposedEmailRecipientAttachment>()
                }
            }
        };
    }


    private static string ComposeSubject(CorrespondenceEntity correspondence)
    {
        return $"Videresendt fra Altinn: {correspondence.Content.MessageTitle}";
    }
    private static string ComposeBody(CorrespondenceEntity correspondence, string sender, string? forwardedBy, string? forwardingText)
    {
        var hasForwardedBy = !string.IsNullOrWhiteSpace(forwardedBy);

        var forwardedByText = hasForwardedBy
            ? $"{forwardedBy} har videresendt en melding til deg fra Altinn."
            : "En melding fra Altinn er videresendt til deg.";

        if (!string.IsNullOrWhiteSpace(forwardingText))
        {
            forwardingText = hasForwardedBy
                ? $"Melding fra {forwardedBy}:\n{forwardingText}"
                : $"Personlig melding:\n{forwardingText}";
        }

        var forwardedReason = hasForwardedBy
            ? $"fordi {forwardedBy} valgte å videresende meldingen"
            : "fordi meldingen ble videresendt til deg";

        var contactText = hasForwardedBy
            ? $"kontakt {sender} eller den som videresendte meldingen"
            : $"kontakt {sender}";

        var forwardedCorrespondenceContent = $"--------- Videresendt melding --------- \n\nFra: {sender}\nMottatt: {correspondence.Created}\nTittel: {correspondence.Content.MessageTitle}\n\n{correspondence.Content.MessageSummary}\n\n{correspondence.Content.MessageBody}";
        var forwardedFooter = $"\n------------------------------------\n\nDenne e-posten er sendt fra Altinn {forwardedReason}. Du kan ikke svare på denne e-posten. Har du spørsmål om innholdet, {contactText}.";
        var formattedComposedEmailBody = $"{forwardedByText}\n\n{forwardingText}\n\n\n\n{forwardedCorrespondenceContent}{forwardedFooter}";
        return formattedComposedEmailBody;
    }

    private async Task<List<ComposedEmailRecipientAttachment>> GetComposedEmailAttachments(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        var attachments = new List<ComposedEmailRecipientAttachment>();

        foreach (var attachment in correspondence.Content.Attachments)
        {
            attachments.Add(new ComposedEmailRecipientAttachment
            {
                FileName = attachment.Attachment.DisplayName ?? attachment.Attachment.FileName,
                MimeType = FileConstants.GetMIMEType(attachment.Attachment.FileName),
                SasUrl = await storageRepository.GenerateSasUrl(attachment.Attachment, attachment.Attachment.StorageProvider, cancellationToken)
            });
        }

        return attachments;
    }
}