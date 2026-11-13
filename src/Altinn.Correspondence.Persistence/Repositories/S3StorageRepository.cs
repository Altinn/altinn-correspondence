using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;

namespace Altinn.Correspondence.Persistence.Repositories;

internal sealed class S3StorageRepository : IStorageRepository, IDisposable
{
    internal const string AttachmentsPrefix = "attachments/";
    internal const string ReportsPrefix = "reports/";
    private const int BlockSize = 32 * 1024 * 1024;
    private const string CorrespondenceCountMetadata = "correspondencecount";
    private const string ServiceOwnerCountMetadata = "serviceownercount";
    private const string ContentMd5Metadata = "contentmd5";

    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _endpoint;
    private readonly ILogger<S3StorageRepository> _logger;

    public S3StorageRepository(IOptions<AttachmentStorageOptions> options, ILogger<S3StorageRepository> logger)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.S3Endpoint)
            || string.IsNullOrWhiteSpace(settings.S3Bucket)
            || string.IsNullOrWhiteSpace(settings.S3AccessKey)
            || string.IsNullOrWhiteSpace(settings.S3SecretKey))
        {
            throw new InvalidOperationException(
                "S3 storage requires AttachmentStorageOptions__S3Endpoint, AttachmentStorageOptions__S3Bucket, AttachmentStorageOptions__S3AccessKey and AttachmentStorageOptions__S3SecretKey.");
        }

        _bucket = settings.S3Bucket;
        _endpoint = settings.S3Endpoint.TrimEnd('/');
        _logger = logger;
        var config = new AmazonS3Config
        {
            ServiceURL = _endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = string.IsNullOrWhiteSpace(settings.S3Region) ? "pl-waw" : settings.S3Region,
            Timeout = TimeSpan.FromHours(1),
            MaxErrorRetry = 3,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        _client = new AmazonS3Client(settings.S3AccessKey, settings.S3SecretKey, config);
    }

    internal static string AttachmentObjectKey(Guid attachmentId) => $"{AttachmentsPrefix}{attachmentId}";

    internal static string ReportObjectKey(string fileName) => $"{ReportsPrefix}{fileName}";

    public async Task<(string locationUrl, string hash, long size)> UploadAttachment(
        AttachmentEntity attachment,
        Stream stream,
        StorageProviderEntity? storageProviderEntity,
        CancellationToken cancellationToken)
    {
        var key = AttachmentObjectKey(attachment.Id);
        string? uploadId = null;
        var parts = new List<PartETag>();
        long totalBytesRead = 0;
        using var hashAlgorithm = MD5.Create();
        using var chunk = new MemoryStream();
        var readBuffer = new byte[1024 * 1024];

        try
        {
            while (true)
            {
                var bytesRead = await stream.ReadAsync(readBuffer.AsMemory(), cancellationToken);
                if (bytesRead > 0)
                {
                    chunk.Write(readBuffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                }

                var finished = bytesRead == 0;
                if (chunk.Length >= BlockSize || (finished && chunk.Length > 0 && uploadId is not null))
                {
                    var part = chunk.ToArray();
                    hashAlgorithm.TransformBlock(part, 0, part.Length, null, 0);
                    uploadId ??= await InitiateMultipartUpload(key, cancellationToken);
                    var etag = await UploadPart(key, uploadId, parts.Count + 1, part, cancellationToken);
                    parts.Add(new PartETag(parts.Count + 1, etag));
                    chunk.SetLength(0);
                }
                else if (finished && chunk.Length > 0)
                {
                    var part = chunk.ToArray();
                    hashAlgorithm.TransformBlock(part, 0, part.Length, null, 0);
                    await PutObject(key, part, metadata: null, cancellationToken);
                    chunk.SetLength(0);
                }

                if (finished)
                {
                    break;
                }
            }

            hashAlgorithm.TransformFinalBlock([], 0, 0);
            if (hashAlgorithm.Hash is null)
            {
                throw new InvalidOperationException("Failed to calculate MD5 hash of uploaded file");
            }

            var hash = Convert.ToHexString(hashAlgorithm.Hash).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(attachment.Checksum)
                && !string.Equals(hash, attachment.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                if (uploadId is not null)
                {
                    await AbortMultipartUpload(key, uploadId, cancellationToken);
                    uploadId = null;
                }
                else
                {
                    await DeleteObject(key, cancellationToken);
                }

                throw new HashMismatchException("Hash mismatch");
            }

            if (uploadId is not null)
            {
                await CompleteMultipartUpload(key, uploadId, parts, cancellationToken);
                uploadId = null;
            }
            else if (totalBytesRead == 0)
            {
                await PutObject(key, [], metadata: null, cancellationToken);
            }

            _logger.LogInformation("Uploaded attachment {AttachmentId} to {ObjectKey}", attachment.Id, key);
            return (ObjectUrl(key), hash, totalBytesRead);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to upload attachment {AttachmentId} to {ObjectKey}", attachment.Id, key);
            if (uploadId is not null)
            {
                await AbortMultipartUpload(key, uploadId, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<Stream> DownloadAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
    {
        return await OpenObject(AttachmentObjectKey(attachmentId), cancellationToken);
    }

    public async Task PurgeAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
    {
        await DeleteObject(AttachmentObjectKey(attachmentId), cancellationToken);
    }

    public async Task<(string locationUrl, string hash, long size)> UploadReportFile(
        string fileName,
        int serviceOwnerCount,
        int correspondenceCount,
        Stream stream,
        CancellationToken cancellationToken)
    {
        await using var content = await CopyToMemory(stream, cancellationToken);
        var hashBytes = MD5.HashData(content.ToArray());
        content.Position = 0;
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var key = ReportObjectKey(fileName);
        await PutObject(key, content.ToArray(), new Dictionary<string, string>
        {
            [CorrespondenceCountMetadata] = correspondenceCount.ToString(),
            [ServiceOwnerCountMetadata] = serviceOwnerCount.ToString(),
            [ContentMd5Metadata] = hashHex
        }, cancellationToken);

        return (ObjectUrl(key), Convert.ToBase64String(hashBytes), content.Length);
    }

    public async Task<(Stream DownloadStream, string FileName, long FileSize, string FileHash, int ServiceOwnerCount, int CorrespondenceCount)> DownloadReportFile(
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = ReportObjectKey(fileName)
            }, cancellationToken);
            var stream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;
            return (
                stream,
                fileName,
                response.ContentLength,
                ReadHash(response.Metadata, response.ETag),
                ReadCount(response.Metadata, ServiceOwnerCountMetadata),
                ReadCount(response.Metadata, CorrespondenceCountMetadata));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Report file '{fileName}' not found in blob storage", exception);
        }
    }

    public async Task<(Stream DownloadStream, string FileName, long FileSize, string FileHash, int ServiceOwnerCount, int CorrespondenceCount)> DownloadLatestReportFile(
        CancellationToken cancellationToken)
    {
        S3Object? latest = null;
        string? continuationToken = null;
        do
        {
            var page = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = ReportsPrefix,
                ContinuationToken = continuationToken
            }, cancellationToken);
            foreach (var item in page.S3Objects ?? [])
            {
                if (item.Key.EndsWith('/'))
                {
                    continue;
                }

                if (latest is null || item.LastModified > latest.LastModified)
                {
                    latest = item;
                }
            }

            continuationToken = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        if (latest is null)
        {
            throw new FileNotFoundException("No report files found in blob storage");
        }

        var fileName = latest.Key[ReportsPrefix.Length..];
        return await DownloadReportFile(fileName, cancellationToken);
    }

    public Task<string> GenerateSasUrl(AttachmentEntity attachment, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken)
    {
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = AttachmentObjectKey(attachment.Id),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddHours(1),
            Protocol = Protocol.HTTPS
        });
        return Task.FromResult(url);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private string ObjectUrl(string key)
    {
        var escapedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return $"{_endpoint}/{_bucket}/{escapedKey}";
    }

    private async Task PutObject(string key, byte[] content, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = new MemoryStream(content, writable: false),
            AutoCloseStream = true,
            UseChunkEncoding = false
        };
        if (metadata is not null)
        {
            foreach (var entry in metadata)
            {
                request.Metadata.Add(entry.Key, entry.Value);
            }
        }

        await _client.PutObjectAsync(request, cancellationToken);
    }

    private async Task<string> InitiateMultipartUpload(string key, CancellationToken cancellationToken)
    {
        var response = await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = _bucket,
            Key = key
        }, cancellationToken);
        return response.UploadId;
    }

    private async Task<string> UploadPart(string key, string uploadId, int partNumber, byte[] content, CancellationToken cancellationToken)
    {
        var response = await _client.UploadPartAsync(new UploadPartRequest
        {
            BucketName = _bucket,
            Key = key,
            UploadId = uploadId,
            PartNumber = partNumber,
            InputStream = new MemoryStream(content, writable: false),
            UseChunkEncoding = false
        }, cancellationToken);
        return response.ETag;
    }

    private async Task CompleteMultipartUpload(string key, string uploadId, List<PartETag> parts, CancellationToken cancellationToken)
    {
        var request = new CompleteMultipartUploadRequest
        {
            BucketName = _bucket,
            Key = key,
            UploadId = uploadId
        };
        request.AddPartETags(parts);
        await _client.CompleteMultipartUploadAsync(request, cancellationToken);
    }

    private async Task AbortMultipartUpload(string key, string uploadId, CancellationToken cancellationToken)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = _bucket,
                Key = key,
                UploadId = uploadId
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to abort multipart upload {UploadId} for {ObjectKey}", uploadId, key);
        }
    }

    private async Task DeleteObject(string key, CancellationToken cancellationToken)
    {
        await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
    }

    private async Task<Stream> OpenObject(string key, CancellationToken cancellationToken)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucket,
            Key = key
        }, cancellationToken);
        return new S3ResponseStream(response);
    }

    private static async Task<MemoryStream> CopyToMemory(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream memoryStream)
        {
            var copy = new MemoryStream();
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            return copy;
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    private static string ReadHash(MetadataCollection metadata, string etag)
    {
        var storedHash = ReadMetadata(metadata, ContentMd5Metadata);
        if (!string.IsNullOrWhiteSpace(storedHash))
        {
            return storedHash;
        }

        var trimmed = etag.Trim('"');
        return trimmed.Length == 32 && !trimmed.Contains('-') ? trimmed : string.Empty;
    }

    private static int ReadCount(MetadataCollection metadata, string key)
    {
        var value = ReadMetadata(metadata, key);
        return int.TryParse(value, out var count) ? count : 0;
    }

    private static string? ReadMetadata(MetadataCollection metadata, string key)
    {
        foreach (var metadataKey in metadata.Keys)
        {
            if (string.Equals(metadataKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return metadata[metadataKey];
            }
        }

        return null;
    }

    private sealed class S3ResponseStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public S3ResponseStream(GetObjectResponse response)
        {
            _response = response;
            _inner = response.ResponseStream;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _response.ContentLength;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
