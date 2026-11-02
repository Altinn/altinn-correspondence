using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Persistence.Helpers;
using Npgsql;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Storage;

public class PostgresRootCertificateTests
{
    [Fact]
    public void Normalize_UnescapesNewlinesFromEnvironmentVariables()
    {
        var normalized = PemText.Normalize("-----BEGIN CERTIFICATE-----\\nQUJD\\n-----END CERTIFICATE-----");

        Assert.Contains("\nQUJD\n", normalized, StringComparison.Ordinal);
        Assert.EndsWith("\n", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_WritesTheConfiguredRootCertificate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "correspondence-pg-ca-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "postgresql-ca.pem");
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "postgres.internal",
            Database = "correspondence",
            Username = "correspondence",
            Password = "secret",
            SslMode = SslMode.Disable,
            RootCertificate = path
        };

        try
        {
            PostgresRootCertificate.Apply(builder, "-----BEGIN CERTIFICATE-----\\nQUJD\\n-----END CERTIFICATE-----");

            Assert.Equal(path, builder.RootCertificate);
            Assert.Equal(SslMode.VerifyCA, builder.SslMode);
            Assert.Contains("QUJD", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
