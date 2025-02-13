using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Hangfire;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class AttachmentHelper(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment, IBackgroundJobClient backgroundJobClient)
    {
        public async Task<OneOf<UploadAttachmentResponse, Error>> UploadAttachment(Stream file, Guid attachmentId, Guid partyUuid, CancellationToken cancellationToken)
        {
            var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
            if (attachment == null)
            {
                return AttachmentErrors.AttachmentNotFound;
            }

            var currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.UploadProcessing, partyUuid, cancellationToken);
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
                    await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.UploadFailed);
                    await storageRepository.PurgeAttachment(attachment.Id, cancellationToken);
                    return AttachmentErrors.UploadFailed;
                }
            }
            catch (DataLocationUrlException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.InvalidLocationUrl);
                return AttachmentErrors.DataLocationNotFound;
            }
            catch (HashMismatchException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.ChecksumMismatch);
                return AttachmentErrors.HashMismatch;
            }
            catch (RequestFailedException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.UploadFailed);
                return AttachmentErrors.UploadFailed;
            }

            if (hostEnvironment.IsDevelopment())
            {
                // Simulate virus scan when running locally
                backgroundJobClient.Enqueue<MalwareScanResultHandler>((handler) => handler.Process(new MalwareScanResult.Models.ScanResultData()
                {
                    ScanResultType = "No threats found",
                    BlobUri = attachment.DataLocationUrl,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ETag = Guid.NewGuid().ToString(),
                    ScanFinishedTimeUtc = DateTime.UtcNow,
                    ScanResultDetails = new MalwareScanResult.Models.ScanResultDetails()
                    {
                        MalwareNamesFound = new List<string>(),
                        Sha256 = Guid.NewGuid().ToString()
                    }
                }, null, cancellationToken));
            }
            return new UploadAttachmentResponse()
            {
                AttachmentId = attachment.Id,
                Status = currentStatus.Status,
                StatusChanged = currentStatus.StatusChanged,
                StatusText = currentStatus.StatusText
            };
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
        public Error? ValidateAttachmentName(AttachmentEntity attachment)
        {
            var filename = attachment.FileName;
            var fileType = Path.GetExtension(filename)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(filename))
            {
                return AttachmentErrors.FilenameMissing;
            }
            if (filename.Length > 255)
            {
                return AttachmentErrors.FilenameTooLong;
            }
            if (fileType == null || !ApplicationConstants.AllowedFileTypes.Contains(fileType))
            {
                return AttachmentErrors.FiletypeNotAllowed;
            }
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                if (filename.Contains(c))
                {
                    return AttachmentErrors.FilenameInvalid;
                }
            }
            return null;
        }
    }
}