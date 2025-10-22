using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Application.MalwareScanResult.Models;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Hangfire;

namespace Altinn.Correspondence.Application.Helpers
{
    public class AttachmentHelper(
        IAttachmentStatusRepository attachmentStatusRepository,
        IAttachmentRepository attachmentRepository,
        IStorageRepository storageRepository,
        IResourceRegistryService resourceRegistryService,
        IServiceOwnerRepository serviceOwnerRepository,
        IHostEnvironment hostEnvironment,
        IBackgroundJobClient backgroundJobClient,
        MalwareScanResultHandler malwareScanResultHandler,
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
            var uploadResult = await UploadBlob(attachment, file, forMigration, partyUuid, cancellationToken);
            if (uploadResult.TryPickT1(out var uploadError, out var successResult))
            {
                return uploadError;
            }
            var (dataLocationUrl, checksum, size) = successResult;
            return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
            {
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
                if (hostEnvironment.IsDevelopment())
                {
                    logger.LogInformation("Development mode detected. Enqueing simulated malware scan result for attachment {attachmentId}", attachmentId);
                    backgroundJobClient.Enqueue<AttachmentHelper>(helper => helper.SimulateMalwareScanResult(attachmentId));
                }

                logger.LogInformation("Finished upload of attachment {attachmentId} for party {partyUuid}", attachmentId, partyUuid);

                return new UploadAttachmentResponse()
                {
                    AttachmentId = attachment.Id,
                    Status = currentStatus.Status,
                    StatusChanged = currentStatus.StatusChanged,
                    StatusText = currentStatus.StatusText
                };
            }, logger, cancellationToken);
        }

        public async Task<StorageProviderEntity> GetStorageProvider(AttachmentEntity attachment, bool forMigration, CancellationToken cancellationToken)
        {
            ServiceOwnerEntity? serviceOwnerEntity = null;
            if (forMigration)
            {
                var serviceOwnerShortHand = attachment.ResourceId.Split('-')[0];
                serviceOwnerEntity = await serviceOwnerRepository.GetServiceOwnerByOrgCode(serviceOwnerShortHand.ToLower(), cancellationToken);
            }
            else
            {
                var serviceOwnerOrgCode = await resourceRegistryService.GetServiceOwnerOrgCode(attachment.ResourceId, cancellationToken);
                if (serviceOwnerOrgCode is null)
                {
                    logger.LogError("Could not find service owner for resource {resourceId}", attachment.ResourceId);
                    return null;
                }
                serviceOwnerEntity = await serviceOwnerRepository.GetServiceOwnerByOrgCode(serviceOwnerOrgCode, cancellationToken);
            }
            if (serviceOwnerEntity == null)
            {
                logger.LogError($"Could not find service owner entity for {attachment.ResourceId} in database");
                //return AttachmentErrors.ServiceOwnerNotFound; // Future PR will add service owner registry as requirement when we have ensured that existing service owners have been provisioned
            }
            return serviceOwnerEntity?.GetStorageProvider(forMigration ? false : true);
        }

        private async Task<OneOf<(string? locationUrl, string? hash, long size),Error>> UploadBlob(AttachmentEntity attachment, Stream stream, bool forMigration, Guid partyUuid, CancellationToken cancellationToken)
        {
            try
            {
                var storageProvider = await GetStorageProvider(attachment, forMigration, cancellationToken);
                var (dataLocationUrl, checksum, size) = await storageRepository.UploadAttachment(attachment, stream, storageProvider, cancellationToken);
                logger.LogInformation("Uploaded {attachmentId} to Azure Storage", attachment.Id);
                return (dataLocationUrl, checksum, size);
            }
            catch (DataLocationUrlException)
            {
                await SetAttachmentStatus(attachment.Id, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.InvalidLocationUrl);
                return AttachmentErrors.DataLocationNotFound;
            }
            catch (HashMismatchException)
            {
                await SetAttachmentStatus(attachment.Id, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.ChecksumMismatch);
                return AttachmentErrors.HashMismatch;
            }
            catch (RequestFailedException)
            {
                await SetAttachmentStatus(attachment.Id, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.UploadFailed);
                return AttachmentErrors.UploadFailed;
            }
        }

        public async Task<AttachmentStatusEntity> SetAttachmentStatus(Guid attachmentId, AttachmentStatus status, Guid partyUuid, CancellationToken cancellationToken, string statusText = null)
        {
            logger.LogInformation("Setting attachment status for {attachmentId} to {status}. Performed by {partyUuid}", attachmentId, status, partyUuid);
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
        /// <summary>
        /// Simulates a malware scan result for local development and tests by calling the MalwareScanResultHandler with fake ScanResultData.
        /// </summary>
        public async Task SimulateMalwareScanResult(Guid attachmentId)
        {
            if (!hostEnvironment.IsDevelopment())
            {
                logger.LogWarning("SimulateMalwareScanResult called outside development environment");
                return;
            }

            logger.LogInformation("Simulating malware scan result for attachment {attachmentId}", attachmentId);

            var simulatedScanResult = new ScanResultData
            {
                BlobUri = $"http://127.0.0.1:10000/devstoreaccount1/attachments/{attachmentId}",
                CorrelationId = Guid.NewGuid().ToString(),
                ETag = "simulated-etag",
                ScanFinishedTimeUtc = DateTime.UtcNow,
                ScanResultDetails = new ScanResultDetails
                {
                    MalwareNamesFound = new List<string>(),
                    Sha256 = "simulated-sha256"
                },
                ScanResultType = "No threats found"
            };

            var result = await malwareScanResultHandler.Process(simulatedScanResult, null, CancellationToken.None);

            if (result.IsT0)
            {
                logger.LogInformation("Successfully simulated malware scan result for attachment {attachmentId} using MalwareScanResultHandler", attachmentId);
            }
            else
            {
                var error = result.AsT1;
                logger.LogError("Error in simulated malware scan result for attachment {attachmentId}: {Error}", attachmentId, error.Message);
            }
        }
    }
}