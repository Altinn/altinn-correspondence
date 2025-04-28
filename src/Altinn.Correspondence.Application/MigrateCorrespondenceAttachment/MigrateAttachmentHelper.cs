using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.MigrateCorrespondenceAttachment
{
    public class MigrateAttachmentHelper(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment, ILogger<MigrateAttachmentHelper> logger)
    {
        public async Task<OneOf<(string DataLocationUrl, string? Checksum, long Size), Error>> UploadAttachment(MigrateAttachmentRequest request, Guid partyUuid, CancellationToken cancellationToken)
        {
            logger.LogInformation("Start upload of attachment {AttachmentId} for party {PartyUuid}", request.Attachment.Id, partyUuid);
            try
            {
                var (dataLocationUrl, checksum, size) = await storageRepository.UploadAttachment(request.Attachment, request.UploadStream, cancellationToken);
                logger.LogInformation("Finished uploaded {AttachmentId} to Azure Storage", request.Attachment.Id);

                return (dataLocationUrl, checksum, size);
            }
            catch (DataLocationUrlException)
            {
                await SetAttachmentStatus(request.Attachment.Id, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.InvalidLocationUrl);
                return AttachmentErrors.DataLocationNotFound;
            }
            catch (HashMismatchException)
            {
                await SetAttachmentStatus(request.Attachment.Id, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.ChecksumMismatch);
                return AttachmentErrors.HashMismatch;
            }
            catch (RequestFailedException)
            {
                await SetAttachmentStatus(request.Attachment.Id, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.UploadFailed);
                return AttachmentErrors.UploadFailed;
            }
        }
        public async Task<AttachmentStatusEntity> SetAttachmentStatus(Guid attachmentId, AttachmentStatus status, Guid partyUuid, CancellationToken cancellationToken, string statusText = null)
        {
            var currentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachmentId,
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = statusText ?? status.ToString(),
                PartyUuid = partyUuid
            };
            await attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
            return currentStatus;
        }
    }
}