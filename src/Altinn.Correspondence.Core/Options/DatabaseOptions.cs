namespace Altinn.Correspondence.Core.Options;

public class DatabaseOptions
{
    public required string ConnectionString { get; set; }

    public string? SslRootCertificatePem { get; set; }
}
