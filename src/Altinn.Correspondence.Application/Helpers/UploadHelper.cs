using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UploadHelper(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment)
    {
        public async Task<OneOf<UploadAttachmentResponse, Error>> UploadAttachment(Stream file, Guid attachmentId, CancellationToken cancellationToken)
        {
            var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
            if (attachment == null)
            {
                return Errors.AttachmentNotFound;
            }

            var currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.UploadProcessing, cancellationToken);
            try
            {
                var (dataLocationUrl, checksum) = await storageRepository.UploadAttachment(attachment, file, cancellationToken);

                var isValidUpdate = await attachmentRepository.SetDataLocationUrl(attachment, AttachmentDataLocationType.AltinnCorrespondenceAttachment, dataLocationUrl, cancellationToken);

                if (string.IsNullOrWhiteSpace(attachment.Checksum))
                {
                    isValidUpdate |= await attachmentRepository.SetChecksum(attachment, checksum, cancellationToken);
                }

                if (!isValidUpdate)
                {
                    await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.UploadFailed);
                    await storageRepository.PurgeAttachment(attachment.Id, cancellationToken);
                    return Errors.UploadFailed;
                }
            }
            catch (DataLocationUrlException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.InvalidLocationUrl);
                return Errors.DataLocationNotFound;
            }
            catch (HashMismatchException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.ChecksumMismatch);
                return Errors.HashError;
            }
            catch (RequestFailedException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.UploadFailed);
                return Errors.UploadFailed;
            }

            if (hostEnvironment.IsDevelopment()) // No malware scan when running locally
            {                
                currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.Published, cancellationToken);
            }

            return new UploadAttachmentResponse()
            {
                AttachmentId = attachment.Id,
                Status = currentStatus.Status,
                StatusChanged = currentStatus.StatusChanged,
                StatusText = currentStatus.StatusText
            };
        }
        private async Task<AttachmentStatusEntity> SetAttachmentStatus(Guid attachmentId, AttachmentStatus status, CancellationToken cancellationToken, string statusText = null)
        {
            var currentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachmentId,
                Status = status,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = statusText ?? status.ToString()
            };
            await attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
            return currentStatus;
        }
    }
}