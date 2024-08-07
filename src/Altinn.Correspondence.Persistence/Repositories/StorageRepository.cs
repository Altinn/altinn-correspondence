using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Persistence.Repositories
{
    internal class StorageRepository(IOptions<AttachmentStorageOptions> options, ILogger<StorageRepository> logger) : IStorageRepository
    {
        private readonly AttachmentStorageOptions _options = options.Value;
        private readonly ILogger<StorageRepository> _logger = logger;

        private BlobClient InitializeBlobClient(Guid fileId)
        {
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

        public async Task<(string locationUrl, string hash)> UploadAttachment(AttachmentEntity attachment, Stream stream, CancellationToken cancellationToken)
        {
            BlobClient blobClient = InitializeBlobClient(attachment.Id);
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
                if (!string.Equals(hash, attachment.Checksum, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new HashMismatchException("Hash mismatch");
                }
                return (locationUrl, hash);
            }
            catch (RequestFailedException requestFailedException)
            {
                _logger.LogError("Error occurred while uploading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                throw;
            }
        }

        public async Task<Stream> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken)
        {
            BlobClient blobClient = InitializeBlobClient(attachmentId);
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

        public async Task PurgeAttachment(Guid attachmentId, CancellationToken cancellationToken)
        {
            BlobClient blobClient = InitializeBlobClient(attachmentId);
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
