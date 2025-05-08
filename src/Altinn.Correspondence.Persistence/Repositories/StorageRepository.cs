using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace Altinn.Correspondence.Persistence.Repositories
{
    internal class StorageRepository(IStorageConnectionStringRepository storageConnectionStringRepository, IOptions<AttachmentStorageOptions> options, ILogger<StorageRepository> logger) : IStorageRepository
    {
        private readonly AttachmentStorageOptions _options = options.Value;
        private readonly ILogger<StorageRepository> _logger = logger;

        private async Task<BlobClient> InitializeBlobClient(Guid fileId, StorageProviderEntity? storageProviderEntity)
        {
            if (storageProviderEntity is not null)
            {
                _logger.LogInformation("Using storage provider: {storageProvider} and resource {storageResourceName}", storageProviderEntity.Id.ToString(), storageProviderEntity.StorageResourceName);
                var connectionString = await storageConnectionStringRepository.GetStorageConnectionString(storageProviderEntity);
                var blobServiceClient = new BlobServiceClient(connectionString,
                    new BlobClientOptions()
                    {
                        Retry =
                            {
                                NetworkTimeout = TimeSpan.FromHours(1),
                            }
                    });
                var containerClient = blobServiceClient.GetBlobContainerClient("attachments");
                BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
                return blobClient;
            } 
            else // Legacy implementation
            {
                _logger.LogInformation("Using Correspondence's storage account");
                var connectionString = _options.ConnectionString;
                var blobServiceClient = new BlobServiceClient(connectionString,
                    new BlobClientOptions()
                    {
                        Retry =
                            {
                                NetworkTimeout = TimeSpan.FromHours(1),
                            }
                    });
                var containerClient = blobServiceClient.GetBlobContainerClient("attachments");
                BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
                return blobClient;
            }
        }

        public async Task<(string locationUrl, string hash, long size)> UploadAttachment(AttachmentEntity attachment, Stream stream, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Uploading attachment with id: {attachmentId} to blob storage. Storage resource: {storageProvider} and resource {storageResourceName}",
                attachment.Id,
                storageProviderEntity?.Id.ToString() ?? "Legacy",
                storageProviderEntity?.StorageResourceName ?? "Legacy");
            BlobClient blobClient = await InitializeBlobClient(attachment.Id, storageProviderEntity);
            var locationUrl = blobClient.Uri.ToString() ?? throw new DataLocationUrlException("Could not get data location url");
            try
            {
                BlobUploadOptions options = new BlobUploadOptions()
                {
                    TransferValidation = new UploadTransferValidationOptions { ChecksumAlgorithm = StorageChecksumAlgorithm.MD5 }
                };
                var blobMetadata = await blobClient.UploadAsync(stream, options, cancellationToken);
                var metadata = blobMetadata.Value;
                var hash = Convert.ToHexString(metadata.ContentHash).ToLowerInvariant();
                var blob = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                var size = blob.Value.ContentLength;
                if (string.IsNullOrWhiteSpace(attachment.Checksum))
                {
                    return (locationUrl, hash, size);
                }
                if (!string.Equals(hash, attachment.Checksum, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new HashMismatchException("Hash mismatch");
                }
                return (locationUrl, hash, size);
            }
            catch (RequestFailedException requestFailedException)
            {
                _logger.LogError("Error occurred while uploading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                throw;
            }
        }

        public async Task<Stream> DownloadAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Downloading attachment with id: {attachmentId} to blob storage. Storage resource: {storageProvider} and resource {storageResourceName}",
                attachmentId,
                storageProviderEntity?.Id.ToString() ?? "Legacy",
                storageProviderEntity?.StorageResourceName ?? "Legacy");
            BlobClient blobClient = await InitializeBlobClient(attachmentId, storageProviderEntity);
            try
            {
                var content = await blobClient.DownloadContentAsync(cancellationToken);
                return content.Value.Content.ToStream();
            }
            catch (RequestFailedException requestFailedException)
            {
                _logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                throw;
            }
        }

        public async Task PurgeAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Purging attachment with id: {attachmentId} to blob storage. Storage resource: {storageProvider} and resource {storageResourceName}",
                attachmentId,
                storageProviderEntity?.Id.ToString() ?? "Legacy",
                storageProviderEntity?.StorageResourceName ?? "Legacy");
            BlobClient blobClient = await InitializeBlobClient(attachmentId, storageProviderEntity);
            try
            {
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException requestFailedException)
            {
                _logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                throw;
            }
        }
    }
}
