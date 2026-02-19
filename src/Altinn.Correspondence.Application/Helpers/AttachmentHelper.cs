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
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Options;

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
        IOptions<GeneralSettings> generalSettings,
        ILogger<AttachmentHelper> logger)
    {

        private static readonly Regex InvalidCharactersRegex = new Regex(
        @"[<>:""/\\|?*\u0000-\u001F]",
        RegexOptions.Compiled
        );

        private static readonly Regex WindowsReservedNamesRegex = new Regex(
        @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        public async Task<OneOf<UploadAttachmentResponse, Error>> UploadAttachment(Stream file, Guid attachmentId, Guid partyUuid, CancellationToken cancellationToken)
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
            var bypassMalwareScan = ShouldBypassMalwareScan(attachment);
            var storageProvider = await GetStorageProvider(attachment, bypassMalwareScan, cancellationToken);
            var uploadResult = await UploadBlob(attachment, file, storageProvider, partyUuid, cancellationToken);
            if (uploadResult.TryPickT1(out var uploadError, out var successResult))
            {
                return uploadError;
            }
            var (dataLocationUrl, checksum, size) = successResult;
            return await TransactionWithRetriesPolicy.Execute<OneOf<UploadAttachmentResponse, Error>>(async (cancellationToken) =>
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
                    currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, partyUuid, cancellationToken, AttachmentStatusText.UploadFailed);
                    await storageRepository.PurgeAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
                    return AttachmentErrors.UploadFailed;
                }
                if (bypassMalwareScan)
                {
                    currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.Published, partyUuid, cancellationToken, "Bypassed malware scan");
                }
                else if (hostEnvironment.IsDevelopment())
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

        private bool ShouldBypassMalwareScan(AttachmentEntity attachment)
        {
            if (string.IsNullOrWhiteSpace(generalSettings.Value.MalwareScanBypassWhiteList))
            {
                return false;
            }
            var whiteList = generalSettings.Value.MalwareScanBypassWhiteList.Split(',').ToList();
            return whiteList.Any(whiteListedResource => attachment.ResourceId == whiteListedResource);
        }

        public async Task<StorageProviderEntity> GetStorageProvider(AttachmentEntity attachment, bool bypassMalwareScan, CancellationToken cancellationToken)
        {
            ServiceOwnerEntity? serviceOwnerEntity = null;
            if (bypassMalwareScan)
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
            return serviceOwnerEntity?.GetStorageProvider(bypassMalwareScan: bypassMalwareScan);
        }

        private async Task<OneOf<(string? locationUrl, string? hash, long size),Error>> UploadBlob(AttachmentEntity attachment, Stream stream, StorageProviderEntity? storageProvider, Guid partyUuid, CancellationToken cancellationToken)
        {
            try
            {
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
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            if (nameWithoutExtension.EndsWith(" ") || nameWithoutExtension.EndsWith("."))
            {
                return AttachmentErrors.FilenameInvalid;
            }
            if (InvalidCharactersRegex.IsMatch(filename))
            {
                return AttachmentErrors.FilenameInvalid;
            }
            if (WindowsReservedNamesRegex.IsMatch(filename))
            {
                return AttachmentErrors.FilenameCannotBeAReservedWindowsFilename;
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

        public Error? ValidateAttachmentExpiration(AttachmentEntity attachment)
        {
            var minimumDays = hostEnvironment.IsProduction() ? 14 : 1;
            if (attachment.ExpirationInDays.HasValue && attachment.ExpirationInDays.Value < minimumDays)
            {
                return AttachmentErrors.AttachmentExpirationPriorMinimumDaysFromNow(minimumDays);
            }
            return null;
        }

        public Error? ValidateAttachmentsExpiration(List<AttachmentEntity> attachments)
        {
            return attachments.Select(ValidateAttachmentExpiration).FirstOrDefault(error => error is not null);
        }

        public Error? ValidateDownloadAttachment(AttachmentEntity attachment)
        {
            if (attachment.StatusHasBeen(AttachmentStatus.Purged))
            {
                return AttachmentErrors.CannotDownloadPurgedAttachment;
            }
            if (attachment.StatusHasBeen(AttachmentStatus.Expired))
            {
                return AttachmentErrors.CannotDownloadExpiredAttachment;
            }
            return null;
        }

        public Error? ValidateDownloadCorrespondenceAttachment(AttachmentEntity attachment, DateTimeOffset? correspondenceAttachmentExpirationTime)
        {
            var baseError = ValidateDownloadAttachment(attachment);
            if (baseError is not null)
            {
                return baseError;
            }

            if (correspondenceAttachmentExpirationTime is DateTimeOffset expirationTime && expirationTime <= DateTimeOffset.UtcNow)
            {
                return AttachmentErrors.CannotDownloadExpiredAttachment;
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