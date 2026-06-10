using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GenerateDelegatedBlobSasUrl;

public class GenerateDelegatedBlobSasUrlHandler(
    IAttachmentRepository attachmentRepository,
    IStorageRepository storageRepository,
    ILogger<GenerateDelegatedBlobSasUrlHandler> logger) : IHandler<GenerateDelegatedBlobSasUrlRequest, GenerateDelegatedBlobSasUrlResponse>
{
    public async Task<OneOf<GenerateDelegatedBlobSasUrlResponse, Error>> Process(
        GenerateDelegatedBlobSasUrlRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating delegated SAS URL for attachment {AttachmentId}", request.AttachmentId);

        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, cancellationToken: cancellationToken);
        if (attachment is null)
        {
            logger.LogWarning("Attachment {AttachmentId} not found", request.AttachmentId);
            return AttachmentErrors.AttachmentNotFound;
        }

        if (string.IsNullOrWhiteSpace(attachment.DataLocationUrl))
        {
            logger.LogWarning("Attachment {AttachmentId} has no data location", request.AttachmentId);
            return AttachmentErrors.DataLocationNotFound;
        }

        try
        {
            var (sasUrl, expiresOn) = await storageRepository.GenerateDelegatedReadSasUrl(
                attachment.Id,
                attachment.StorageProvider,
                cancellationToken);

            logger.LogInformation("Generated delegated SAS URL for attachment {AttachmentId}, expires {ExpiresOn}", request.AttachmentId, expiresOn);

            return new GenerateDelegatedBlobSasUrlResponse
            {
                SasUrl = sasUrl,
                ExpiresOn = expiresOn
            };
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Delegated SAS is not supported for attachment {AttachmentId}", request.AttachmentId);
            return AttachmentErrors.DelegatedSasNotSupported;
        }
    }
}
