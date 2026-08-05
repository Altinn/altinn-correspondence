using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Azure.Storage.Blobs.Specialized;

namespace Altinn.Correspondence.Application.Helpers;

public class ComposedEmailHelper(
    IStorageRepository storageRepository
)
{
    public async Task<ComposedEmailRequest> MapToComposedEmailRequest(CorrespondenceEntity correspondence, string forwardTo, string? forwardingText, CancellationToken cancellationToken)
    {
        return new ComposedEmailRequest
        {
            IdempotencyId = Guid.NewGuid(),
            SendersReference = correspondence.SendersReference,
            Recipient = new ComposedEmailRecipient
            {
                EmailAddress = forwardTo,
                EmailSettings = new ComposedEmailRecipientSettings
                {
                    Subject = correspondence.Content.MessageTitle,
                    Body = ComposeBody(correspondence, forwardingText),
                    ContentType = "Plain",
                    Attachments = await GetComposedEmailAttachments(correspondence, cancellationToken)
                }
            }
        };
    }

    private static string ComposeBody(CorrespondenceEntity correspondence, string? forwardingText)
    {
        if (string.IsNullOrWhiteSpace(forwardingText))
        {
            return correspondence.Content.MessageBody;
        }

        return $"{forwardingText}\n\n---\n\n{correspondence.Content.MessageBody}";
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