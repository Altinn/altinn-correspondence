using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
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
        ILogger<MigrateToStorageProviderHandler> logger) : IHandler<string, bool>
    {
        public async Task<OneOf<bool, Error>> Process(string resourceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
        {
            var serviceOwnerId = await resourceRegistryService.GetServiceOwnerOrganizationId(resourceId, cancellationToken);
            if (string.IsNullOrWhiteSpace(serviceOwnerId))
            {
                return new Error(0, $"Service owner not found for resource {resourceId} in registry", System.Net.HttpStatusCode.NotFound);
            }
            var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId.WithoutPrefix(), cancellationToken);
            if (serviceOwner == null)
            {
                return new Error(1, $"Service owner not found in database. Run sql command 'select initialize_service_owner('{serviceOwnerId}', 'orgShortHandName')' with correct orgShortHandName", System.Net.HttpStatusCode.NotFound);
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
                await ProcessSingle(attachment.Id, storageProviderId);
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

            var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerOrgNo, CancellationToken.None);
            if (serviceOwner is null)
            {
                throw new ArgumentException($"Service owner with id {serviceOwnerOrgNo} not found", nameof(serviceOwnerOrgNo));
            }
            var storageProvider = serviceOwner.StorageProviders.FirstOrDefault(sp => sp.Type == Core.Models.Enums.StorageProviderType.Altinn3Azure);
            if (storageProvider is null)
            {
                throw new ArgumentException($"Storage provider not found for service owner {serviceOwnerOrgNo}", nameof(serviceOwnerOrgNo));
            }

            try { 
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
