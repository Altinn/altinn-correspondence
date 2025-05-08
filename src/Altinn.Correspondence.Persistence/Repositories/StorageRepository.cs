using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Core;
using System.Collections.Concurrent;

namespace Altinn.Correspondence.Persistence.Repositories
{
    internal class StorageRepository : IStorageRepository
    {
        private readonly AttachmentStorageOptions _options;
        private readonly ILogger<StorageRepository> _logger;
        private readonly ConcurrentDictionary<string, BlobServiceClient> _blobServiceClients;
        private readonly BlobClientOptions _blobClientOptions;

        public StorageRepository(IOptions<AttachmentStorageOptions> options, ILogger<StorageRepository> logger)
        {
            _options = options.Value;
            _logger = logger;
            _blobServiceClients = new ConcurrentDictionary<string, BlobServiceClient>();
            _blobClientOptions = new BlobClientOptions()
            {
                Retry =
                {
                    NetworkTimeout = TimeSpan.FromHours(1),
                }
            };
        }

        private BlobServiceClient GetOrCreateBlobServiceClient(string storageResourceName)
        {
            return _blobServiceClients.GetOrAdd(storageResourceName, key =>
            {
                _logger.LogInformation("Creating BlobServiceClient for {storageResourceName}", storageResourceName);
                var storageUri = new Uri($"https://{storageResourceName}.blob.core.windows.net");
                return new BlobServiceClient(storageUri, new DefaultAzureCredential(), _blobClientOptions);
            });
        }

        private async Task<BlobClient> InitializeBlobClient(Guid fileId, StorageProviderEntity? storageProviderEntity)
        {
            string storageResourceName;

            if (storageProviderEntity is not null)
            {
                _logger.LogInformation("Using storage provider: {storageProvider} and resource {storageResourceName}",
                    storageProviderEntity.Id.ToString(), storageProviderEntity.StorageResourceName);
                var blobServiceClient = GetOrCreateBlobServiceClient(storageProviderEntity.StorageResourceName);
                var containerClient = blobServiceClient.GetBlobContainerClient("attachments");
                return containerClient.GetBlobClient(fileId.ToString());
            }
            else // Legacy implementation
            {
                _logger.LogInformation("Using Correspondence's storage account");
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

        public static string GetAccountNameFromConnectionString(string connectionString)
        {
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("AccountName="))
                {
                    return part.Substring("AccountName=".Length);
                }
            }
            return null;
        }

        public async Task<(string locationUrl, string hash, long size)> UploadAttachment(AttachmentEntity attachment, Stream stream, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            logger.LogInformation(
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
                logger.LogError("Error occurred while uploading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                throw;
            }
        }

        public async Task<Stream> DownloadAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            logger.LogInformation(
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
                logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                throw;
            }
        }

        public async Task PurgeAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            logger.LogInformation(
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
                logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                throw;
            }
        }
    }
}
