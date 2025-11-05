using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Altinn.Correspondence.Persistence.Repositories
{
    internal class StorageRepository : IStorageRepository
    {
        private readonly AttachmentStorageOptions _options;
        private readonly ILogger<StorageRepository> _logger;
        private readonly ConcurrentDictionary<string, BlobServiceClient> _blobServiceClients;
        private readonly BlobClientOptions _blobClientOptions;

        private const int BLOCK_SIZE = 32 * 1024 * 1024; // 32 MB
        private const int CONCURRENT_UPLOAD_THREADS = 3;
        private const int BLOCKS_BEFORE_COMMIT = 1000;

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
            var blobContainerClient = await GetBlobContainerClient(fileId, storageProviderEntity);
            return blobContainerClient.GetBlobClient(fileId.ToString());
        }


        private async Task<BlobContainerClient> GetBlobContainerClient(Guid fileId, StorageProviderEntity? storageProviderEntity)
        {
            string storageResourceName;

            if (storageProviderEntity is not null)
            {
                var blobServiceClient = GetOrCreateBlobServiceClient(storageProviderEntity.StorageResourceName);
                return blobServiceClient.GetBlobContainerClient("attachments");
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
                return blobServiceClient.GetBlobContainerClient("attachments");
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
            _logger.LogInformation($"Starting upload of {attachment.Id} for {storageProviderEntity?.ServiceOwnerId}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var blobContainerClient = await GetBlobContainerClient(attachment.Id, storageProviderEntity);
            BlockBlobClient blockBlobClient = blobContainerClient.GetBlockBlobClient(attachment.Id.ToString());
            try
            {
                using var accumulationBuffer = new MemoryStream();
                var networkReadBuffer = new byte[1024 * 1024];
                var blockList = new List<string>();
                long totalBytesRead = 0;
                using var blobMd5 = MD5.Create();

                int blocksInBatch = 0;
                var uploadTasks = new List<Task>();
                using var semaphore = new SemaphoreSlim(CONCURRENT_UPLOAD_THREADS);

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(networkReadBuffer, 0, networkReadBuffer.Length, cancellationToken);
                    if (bytesRead <= 0)
                    {
                        // Handle any remaining data in accumulation buffer
                        if (accumulationBuffer.Length > 0)
                        {
                            accumulationBuffer.Position = 0;
                            var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                            byte[] blockData = accumulationBuffer.ToArray();
                            blobMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);

                            blockList.Add(blockId);
                            blocksInBatch++;

                            await semaphore.WaitAsync(cancellationToken);
                            uploadTasks.Add(UploadBlock(blockBlobClient, blockId, blockData, cancellationToken));
                        }
                        break; // End of stream
                    }

                    accumulationBuffer.Write(networkReadBuffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (accumulationBuffer.Length >= BLOCK_SIZE)
                    {
                        accumulationBuffer.Position = 0;
                        var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                        byte[] blockData = accumulationBuffer.ToArray();
                        blobMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);

                        blockList.Add(blockId);
                        blocksInBatch++;
                        accumulationBuffer.SetLength(0); // Clear accumulation buffer for next block

                        await semaphore.WaitAsync(cancellationToken);
                        uploadTasks.Add(UploadBlockAsync(blockBlobClient, blockId, blockData, cancellationToken));

                        async Task UploadBlockAsync(BlockBlobClient client, string currentBlockId, byte[] currentBlockData, CancellationToken cancellationToken)
                        {
                            try
                            {
                                int currentBlock = blocksInBatch;
                                await UploadBlock(client, currentBlockId, currentBlockData, cancellationToken);

                                var uploadSpeedMBps = totalBytesRead / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                                _logger.LogInformation($"Uploaded block {currentBlock}. Progress: " +
                                    $"{totalBytesRead / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSpeedMBps:N2} MB/s)");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }

                        if (uploadTasks.Count >= BLOCKS_BEFORE_COMMIT)
                        {
                            await Task.WhenAll(uploadTasks);

                            // Commit the blocks we have so far without MD5 hash
                            var blocksToCommit = blockList.ToList();
                            var isFirstCommit = blockList.Count <= BLOCKS_BEFORE_COMMIT;
                            await CommitBlocks(blockBlobClient, blocksToCommit, firstCommit: isFirstCommit, null, cancellationToken);

                            uploadTasks.Clear();
                        }
                    }
                }

                await Task.WhenAll(uploadTasks);

                // Calculate final MD5
                blobMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (blobMd5.Hash is null)
                {
                    throw new Exception("Failed to calculate MD5 hash of uploaded file");
                }
                await CommitBlocks(blockBlobClient, blockList, firstCommit: blockList.Count <= BLOCKS_BEFORE_COMMIT, null, cancellationToken);

                double finalSpeedMBps = totalBytesRead / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                _logger.LogInformation($"Successfully uploaded {totalBytesRead / (1024.0 * 1024.0 * 1024.0):N2} GiB " +
                    $"in {stopwatch.ElapsedMilliseconds / 1000.0:N1}s (avg: {finalSpeedMBps:N2} MB/s)");

                var hash = BitConverter.ToString(blobMd5.Hash).Replace("-", "").ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(attachment.Checksum) && !string.Equals(hash, attachment.Checksum, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new HashMismatchException("Hash mismatch");
                }
                return (blockBlobClient.Uri.ToString(), hash, totalBytesRead);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error occurred while uploading file: {errorMessage}: {stackTrace} ", ex.Message, ex.StackTrace);
                await blockBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                throw;
            }
        }

        private async Task UploadBlock(BlockBlobClient client, string blockId, byte[] blockData, CancellationToken cancellationToken)
        {
            await BlobRetryPolicy.ExecuteAsync(_logger, async () =>
            {
                _logger.LogInformation("Uploading block " + blockId);
                using var blockMd5 = MD5.Create();
                using var blockStream = new MemoryStream(blockData, writable: false);
                blockStream.Position = 0;
                var blockResponse = await client.StageBlockAsync(
                    blockId,
                    blockStream,
                    blockMd5.ComputeHash(blockData),
                    conditions: null,
                    null,
                    cancellationToken: cancellationToken
                );
                if (blockResponse.GetRawResponse().Status != 201)
                {
                    throw new Exception($"Failed to upload block {blockId}: {blockResponse.GetRawResponse().Content}");
                }
            });
        }

        private async Task CommitBlocks(BlockBlobClient client, List<string> blockList, bool firstCommit, byte[]? finalMd5,
            CancellationToken cancellationToken)
        {
            await BlobRetryPolicy.ExecuteAsync(_logger, async () =>
            {
                _logger.LogInformation($"Committing {blockList.Count} blocks");
                var options = new CommitBlockListOptions
                {
                    // Only use ifNoneMatch for the first commit to ensure concurrent upload attempts do not work simultaneously
                    Conditions = null,
                    HttpHeaders = finalMd5 is null ? null : new BlobHttpHeaders
                    {
                        ContentHash = finalMd5
                    }
                };
                var response = await client.CommitBlockListAsync(blockList, options, cancellationToken);
                _logger.LogInformation($"Committed {blockList.Count} blocks: {response.GetRawResponse().ReasonPhrase}");
            });
        }

        public async Task<Stream> DownloadAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
            BlobClient blobClient = await InitializeBlobClient(attachmentId, storageProviderEntity);

            try
            {
                // Stream directly from blob to avoid loading entire content into memory
                var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false)
                {
                    BufferSize = 4 * 1024 * 1024 // 4 MiB buffer
                }, cancellationToken);
                return stream;
            }
            catch (RequestFailedException requestFailedException)
            {
                _logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
                throw;
            }
        }

        public async Task PurgeAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
        {
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

        public async Task<(string locationUrl, string hash, long size)> UploadReportFile(string fileName, Stream stream, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting upload of report file: {fileName}", fileName);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Use the legacy implementation for reports (Correspondence's storage account)
                var connectionString = _options.ConnectionString;
                var blobServiceClient = new BlobServiceClient(connectionString, _blobClientOptions);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient("reports");
                
                // Ensure the reports container exists
                await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                
                var blobClient = blobContainerClient.GetBlobClient(fileName);
                
                // Calculate MD5 hash
                using var md5 = MD5.Create();
                var hash = Convert.ToBase64String(md5.ComputeHash(stream));
                stream.Position = 0; // Reset stream position after hash calculation
                
                // Upload the file
                var response = await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
                
                stopwatch.Stop();
                _logger.LogInformation("Successfully uploaded report file {fileName} in {elapsedMs}ms", fileName, stopwatch.ElapsedMilliseconds);
                
                return (blobClient.Uri.ToString(), hash, stream.Length);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to upload report file {fileName} after {elapsedMs}ms", fileName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

    public async Task<Stream> DownloadReportFile(string fileName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting download of report file: {fileName}", fileName);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Use the same connection string as for uploads
            var connectionString = _options.ConnectionString;
            var blobServiceClient = new BlobServiceClient(connectionString, _blobClientOptions);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("reports");
            
            var blobClient = blobContainerClient.GetBlobClient(fileName);
            
            // Check if the blob exists
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                throw new FileNotFoundException($"Report file '{fileName}' not found in blob storage");
            }
            
            // Stream directly from blob to avoid large in-memory buffers
            var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false)
            {
                BufferSize = 4 * 1024 * 1024 // 4 MiB buffer
            }, cancellationToken);
            
            stopwatch.Stop();
            _logger.LogInformation("Successfully downloaded report file {fileName} in {elapsedMs}ms", fileName, stopwatch.ElapsedMilliseconds);
            
            return stream;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to download report file {fileName} after {elapsedMs}ms", fileName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

internal static class BlobRetryPolicy
{
    private static IAsyncPolicy RetryWithBackoff(ILogger logger) => Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (ex, timeSpan) => {
                logger.LogWarning($"Error during retries: {ex.Message}");
            }
        );

    public static Task ExecuteAsync(ILogger logger, Func<Task> action) => RetryWithBackoff(logger).ExecuteAsync(action);
}
}
