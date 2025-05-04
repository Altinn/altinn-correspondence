using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class AttachmentHelper(
        IAttachmentStatusRepository attachmentStatusRepository,
        IAttachmentRepository attachmentRepository,
        IStorageRepository storageRepository,
        IResourceRegistryService resourceRegistryService,
        IServiceOwnerRepository serviceOwnerRepository,
        IHostEnvironment hostEnvironment,
        ILogger<AttachmentHelper> logger)
    {
        public async Task<OneOf<UploadAttachmentResponse, Error>> UploadAttachment(Stream file, Guid attachmentId, Guid partyUuid, bool forMigration, CancellationToken cancellationToken)
        {
            logger.LogInformation("Start upload of attachment {attachmentId} for party {partyUuid}", attachmentId, partyUuid);
            var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
            if (attachment == null)
            {
                return AttachmentErrors.AttachmentNotFound;
            }
            logger.LogInformation("Retrieved attachment {attachmentId} from db", attachmentId);

            var currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.UploadProcessing, partyUuid, cancellationToken);
            logger.LogInformation("Set attachment status of {attachmentId} to UploadProcessing", attachmentId);
            try
            {
                var serviceOwnerId = await resourceRegistryService.GetServiceOwnerOfResource(attachment.ResourceId, cancellationToken);
                if (serviceOwnerId is null)
                {
                    logger.LogError("Could not find service owner for resource {resourceId}", attachment.ResourceId);
                    return AttachmentErrors.ResourceRegistryLookupFailed;
                }

                var serviceOwnerEntity = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId, cancellationToken);
                if (serviceOwnerEntity == null)
                {
                    logger.LogError("Could not find service owner entity for {serviceOwnerId} in database", serviceOwnerId);
                    //return AttachmentErrors.ServiceOwnerNotFound; // Future PR will add service owner registry as requirement when we have ensured that existing service owners have been provisioned
                }
                var storageProvider = serviceOwnerEntity?.GetStorageProvider(forMigration ? false : true);
                var (dataLocationUrl, checksum, size) = await storageRepository.UploadAttachment(attachment, file, storageProvider, cancellationToken);
                logger.LogInformation("Uploaded {attachmentId} to Azure Storage", attachmentId);

                var isValidUpdate = await attachmentRepository.SetDataLocationUrl(attachment, AttachmentDataLocationType.AltinnCorrespondenceAttachment, dataLocationUrl, storageProvider, cancellationToken);
                logger.LogInformation("Set dataLocationUrl of {attachmentId}", attachmentId);

                if (string.IsNullOrWhiteSpace(attachment.Checksum))
                {
                    isValidUpdate |= await attachmentRepository.SetChecksum(attachment, checksum, cancellationToken);
                }
                isValidUpdate |= await attachmentRepository.SetAttachmentSize(attachment, size, cancellationToken);

                if (!isValidUpdate)
                {
                    await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.UploadFailed);
                    await storageRepository.PurgeAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
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

            if (hostEnvironment.IsDevelopment()) // No malware scan when running locally
            {
                currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.Published, partyUuid, cancellationToken);
            }
            logger.LogInformation("Finished upload of attachment {attachmentId} for party {partyUuid}", attachmentId, partyUuid);

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