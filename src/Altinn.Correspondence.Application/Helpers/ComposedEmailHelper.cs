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
    public async Task<ComposedEmailRequest> MapToComposedEmailRequest(CorrespondenceEntity correspondence, string forwardTo, CancellationToken cancellationToken)
    {
        return new ComposedEmailRequest
        {
            IdempotencyId = Guid.NewGuid(),
            SendersReference = correspondence.SendersReference,
            RequestedSendTime = DateTimeOffset.UtcNow.AddSeconds(10),
            Recipient = new ComposedEmailRecipient
            {
                EmailAddress = forwardTo,
                EmailSettings = new ComposedEmailRecipientSettings
                {
                    Subject = correspondence.Content.MessageTitle,
                    Body = correspondence.Content.MessageBody,
                    ContentType = "Plain",
                    Attachments = await GetComposedEmailAttachments(correspondence, cancellationToken)
                }
            }
        };
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