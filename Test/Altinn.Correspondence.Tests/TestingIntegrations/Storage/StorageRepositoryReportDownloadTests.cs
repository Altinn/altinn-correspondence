using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Persistence.Repositories;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Storage;

public class StorageRepositoryReportDownloadTests
{
    private const string DevelopmentStorageConnectionString = "UseDevelopmentStorage=true";

    [Fact]
    public async Task DownloadReportFile_WhenBlobIsOverwrittenBetweenPropertiesAndOpenRead_OpenReadBoundToPropertiesETagFails()
    {
        if (!await IsAzuriteAvailableAsync())
        {
            return; // Azurite not running locally/CI — skip without failing the suite
        }

        var containerClient = new BlobServiceClient(DevelopmentStorageConnectionString)
            .GetBlobContainerClient("reports");
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient($"etag-race-test-{Guid.NewGuid():N}.parquet");

        await blobClient.UploadAsync(
            new MemoryStream("original-report-content"u8.ToArray()),
            overwrite: true);

        try
        {
            // Mirror StorageRepository.DownloadReportFile: properties first, then OpenRead with IfMatch.
            var properties = await blobClient.GetPropertiesAsync();
            var propertiesEtag = properties.Value.ETag;

            await blobClient.UploadAsync(
                new MemoryStream("overwritten-report-content"u8.ToArray()),
                overwrite: true);

            var exception = await Assert.ThrowsAsync<RequestFailedException>(async () =>
            {
                await using var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false)
                {
                    Conditions = new BlobRequestConditions { IfMatch = propertiesEtag }
                });
                _ = stream.ReadByte();
            });

            Assert.Equal(412, exception.Status);

            // Repository download after overwrite should succeed against the current version only.
            var options = Options.Create(new AttachmentStorageOptions
            {
                ConnectionString = DevelopmentStorageConnectionString
            });
            var repository = new StorageRepository(options, NullLogger<StorageRepository>.Instance);
            var download = await repository.DownloadReportFile(blobClient.Name, CancellationToken.None);
            await using var downloadStream = download.DownloadStream;
            using var reader = new StreamReader(downloadStream);
            Assert.Equal("overwritten-report-content", await reader.ReadToEndAsync());
        }
        finally
        {
            await blobClient.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task DownloadReportFile_UsesPropertiesETagForOpenRead_AndReturnsMatchingMetadata()
    {
        if (!await IsAzuriteAvailableAsync())
        {
            return;
        }

        var options = Options.Create(new AttachmentStorageOptions
        {
            ConnectionString = DevelopmentStorageConnectionString
        });
        var repository = new StorageRepository(options, NullLogger<StorageRepository>.Instance);

        var fileName = $"etag-ok-test-{Guid.NewGuid():N}.parquet";
        await using var content = new MemoryStream("stable-report-content"u8.ToArray());
        await repository.UploadReportFile(fileName, serviceOwnerCount: 2, correspondenceCount: 5, content, CancellationToken.None);

        try
        {
            var download = await repository.DownloadReportFile(fileName, CancellationToken.None);
            await using var downloadStream = download.DownloadStream;
            using var reader = new StreamReader(downloadStream);
            var text = await reader.ReadToEndAsync();

            Assert.Equal(fileName, download.FileName);
            Assert.Equal(2, download.ServiceOwnerCount);
            Assert.Equal(5, download.CorrespondenceCount);
            Assert.Equal("stable-report-content", text);
            Assert.False(string.IsNullOrWhiteSpace(download.FileHash));
        }
        finally
        {
            var cleanupClient = new BlobServiceClient(DevelopmentStorageConnectionString)
                .GetBlobContainerClient("reports")
                .GetBlobClient(fileName);
            await cleanupClient.DeleteIfExistsAsync();
        }
    }

    private static async Task<bool> IsAzuriteAvailableAsync()
    {
        try
        {
            var client = new BlobServiceClient(DevelopmentStorageConnectionString);
            await client.GetPropertiesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
