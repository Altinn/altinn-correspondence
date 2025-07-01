using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Collections.Concurrent;

namespace Altinn.Correspondence.Application.MigrateCorrespondenceAttachment
{
    public class MigrateAttachmentHelper(
        IAttachmentStatusRepository attachmentStatusRepository,
        IAttachmentRepository attachmentRepository,
        IStorageRepository storageRepository,
        IServiceOwnerRepository serviceOwnerRepository,
        IHostEnvironment hostEnvironment,
        ILogger<MigrateAttachmentHelper> logger)
    {
        private static readonly ConcurrentDictionary<string, StorageProviderEntity?> _providerCache = new();

        public async Task<StorageProviderEntity> GetStorageProvider(AttachmentEntity attachment, CancellationToken cancellationToken)
        {
            var serviceOwnerShortHand = attachment.ResourceId.Split('-')[0].ToLower();
            StorageProviderEntity? storageProvider = _providerCache.GetOrAdd(serviceOwnerShortHand, so =>
            {
                ServiceOwnerEntity? serviceOwnerEntity = serviceOwnerRepository.GetServiceOwnerByOrgCode(so, cancellationToken).Result;
                if (serviceOwnerEntity == null)
                {
                    logger.LogError($"Could not find service owner entity for {attachment.ResourceId} in database");
                    //return AttachmentErrors.ServiceOwnerNotFound; // Future PR will add service owner registry as requirement when we have ensured that existing service owners have been provisioned
                }

                return serviceOwnerEntity?.GetStorageProvider(false);
            });

            return storageProvider;
        }

        public async Task<OneOf<(string DataLocationUrl, string? Checksum, long Size, StorageProviderEntity StorageProviderEntity), Error>> UploadAttachment(MigrateAttachmentRequest request, Guid partyUuid, CancellationToken cancellationToken)
        {
            try
            {
                var provider = await GetStorageProvider(request.Attachment, cancellationToken);
                var (dataLocationUrl, checksum, size) = await storageRepository.UploadAttachment(request.Attachment, request.UploadStream, provider, cancellationToken);

                return (dataLocationUrl, checksum, size, provider);
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