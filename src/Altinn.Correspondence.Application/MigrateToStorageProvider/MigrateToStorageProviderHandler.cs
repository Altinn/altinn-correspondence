using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.MigrateToStorageProvider
{
    public class MigrateToStorageProviderHandler(
        IServiceOwnerRepository serviceOwnerRepository,
        IAttachmentRepository attachmentRepository,
        IResourceRegistryService resourceRegistryService,
        IStorageRepository storageRepository,
        AttachmentHelper attachmentHelper,
        BackgroundJobClient backgroundJobClient,
        ILogger<MigrateToStorageProviderHandler> logger) : IHandler<string, bool>
    {
        public async Task<OneOf<bool, Error>> Process(string resourceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
        {
            ServiceOwnerEntity? serviceOwner = null;
            if (resourceId.Contains("migratedcorrespondence"))
            {
                var serviceOwnerShortHand = resourceId.Split('-')[0];
                serviceOwner = await serviceOwnerRepository.GetServiceOwnerFromOrgCode(serviceOwnerShortHand.ToLower(), cancellationToken);
            }
            else
            {
                var serviceOwnerId = await resourceRegistryService.GetServiceOwnerOrganizationId(resourceId, cancellationToken);
                if (serviceOwnerId is null)
                {
                    logger.LogError("Could not find service owner for resource {resourceId}", resourceId);
                    return null;
                }
                serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId, cancellationToken);
            }
            var attachmentsWithoutStorageProvider = await attachmentRepository.GetAttachmentsByResourceIdWithoutStorageProvider(resourceId, cancellationToken);
            if (attachmentsWithoutStorageProvider == null || attachmentsWithoutStorageProvider.Count == 0)
            {
                return new Error(2, $"No attachments found for resource {resourceId} without storage provider", System.Net.HttpStatusCode.NotFound);
            }

            for(var i = 0; i < attachmentsWithoutStorageProvider.Count; i++)
            {
                var attachment = attachmentsWithoutStorageProvider[i];
                var storageProviderId = serviceOwner.StorageProviders.FirstOrDefault(sp => sp.Type == Core.Models.Enums.StorageProviderType.Altinn3Azure)?.ServiceOwnerId;
                if (storageProviderId is null)
                {
                    return new Error(3, $"No storage provider found for attachment {attachment.Id} of resource {resourceId}", System.Net.HttpStatusCode.NotFound);
                }
                backgroundJobClient.Enqueue<MigrateToStorageProviderHandler>((handler) => handler.ProcessSingle(attachment.Id, storageProviderId));
            }
            return true;
        }

        public async Task ProcessSingle(Guid attachmentId, string serviceOwnerOrgNo)
        {
            var attachment = await attachmentRepository.GetAttachmentById(attachmentId, false, CancellationToken.None);
            if (attachment is null)
            {
                throw new ArgumentException($"Attachment with id {attachmentId} not found", nameof(attachmentId));
            }

            if (attachment.StorageProvider is not null)
            {
                throw new ArgumentException($"Storage provider already set on attachment {attachmentId}");
            }
            var storageProvider = await attachmentHelper.GetStorageProvider(attachment, attachment.ResourceId.Contains("migratedcorrespondence"), CancellationToken.None);
            if (storageProvider is null)
            {
                throw new ArgumentException($"Storage provider not found for service owner {serviceOwnerOrgNo}", nameof(serviceOwnerOrgNo));
            }

            try 
            { 
                var fileStream = await storageRepository.DownloadAttachment(attachmentId, null, CancellationToken.None);
                if (fileStream is null)
                {
                    throw new ArgumentException($"Attachment with id {attachmentId} not found in storage", nameof(attachmentId));
                }

                var uploadResult = await storageRepository.UploadAttachment(attachment, fileStream, storageProvider, CancellationToken.None);
                if (attachment.AttachmentSize == 0)
                {
                    attachment.AttachmentSize = uploadResult.size;
                    await attachmentRepository.SetAttachmentSize(attachment, uploadResult.size, CancellationToken.None);
                }
                if (uploadResult.size != attachment.AttachmentSize)
                {
                    throw new Exception($"Unsuccessful! Uploaded file size differed from one defined on attachment. Got {uploadResult.size} but expected {attachment.AttachmentSize}");
                }
                await attachmentRepository.SetStorageProvider(attachmentId, storageProvider, uploadResult.locationUrl, CancellationToken.None);
            } 
            catch (Exception e)
            {
                logger.LogWarning($"Could not process attachment {attachmentId}: {e.Message}");
            }
        }
    }
}
