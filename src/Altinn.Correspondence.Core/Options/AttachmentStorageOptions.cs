namespace Altinn.Correspondence.Core.Options;

public class AttachmentStorageOptions
{
    public required string ConnectionString { get; set; }

    public string? S3Endpoint { get; set; }

    public string? S3Region { get; set; }

    public string? S3Bucket { get; set; }

    public string? S3AccessKey { get; set; }

    public string? S3SecretKey { get; set; }
}
