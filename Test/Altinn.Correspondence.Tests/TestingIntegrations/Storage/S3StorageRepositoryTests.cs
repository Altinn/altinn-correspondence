using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Storage;

public class S3StorageRepositoryTests
{
    [Fact]
    public void Create_RequiresBucketAndCredentials()
    {
        var options = Options.Create(new AttachmentStorageOptions
        {
            ConnectionString = "",
            S3Endpoint = "https://s3.pl-waw.scw.cloud"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new S3StorageRepository(options, NullLogger<S3StorageRepository>.Instance));

        Assert.Contains("AttachmentStorageOptions__S3AccessKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateSasUrl_SignsAOneHourReadUrlForTheAttachment()
    {
        var attachmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var repository = new S3StorageRepository(Options.Create(new AttachmentStorageOptions
        {
            ConnectionString = "",
            S3Endpoint = "https://s3.pl-waw.scw.cloud",
            S3Region = "pl-waw",
            S3Bucket = "correspondence-attachments",
            S3AccessKey = "access-key",
            S3SecretKey = "secret-key"
        }), NullLogger<S3StorageRepository>.Instance);

        var url = await repository.GenerateSasUrl(new AttachmentEntity
        {
            Id = attachmentId,
            ResourceId = "resource",
            FileName = "file.txt",
            SendersReference = "reference",
            Sender = "0192:910753614"
        }, null, CancellationToken.None);

        Assert.StartsWith("https://s3.pl-waw.scw.cloud/correspondence-attachments/attachments/", url, StringComparison.Ordinal);
        Assert.Contains(attachmentId.ToString(), url, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Expires=3600", url, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectKeys_UseAttachmentAndReportPrefixes()
    {
        var attachmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal($"attachments/{attachmentId}", S3StorageRepository.AttachmentObjectKey(attachmentId));
        Assert.Equal("reports/daily.parquet", S3StorageRepository.ReportObjectKey("daily.parquet"));
    }
}
