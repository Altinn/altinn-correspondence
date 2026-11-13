using Altinn.Correspondence.Common.Helpers;
using Npgsql;

namespace Altinn.Correspondence.Persistence.Helpers;

internal static class PostgresRootCertificate
{
    public static void Apply(NpgsqlConnectionStringBuilder builder, string? pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            return;
        }

        var normalized = PemText.Normalize(pem);
        var path = string.IsNullOrWhiteSpace(builder.RootCertificate)
            ? Path.Combine(Path.GetTempPath(), "altinn-correspondence-postgresql-ca.pem")
            : builder.RootCertificate;

        try
        {
            Write(path, normalized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            path = Path.Combine(Path.GetTempPath(), "altinn-correspondence-postgresql-ca.pem");
            Write(path, normalized);
        }

        builder.RootCertificate = path;
        if (builder.SslMode < SslMode.VerifyCA)
        {
            builder.SslMode = SslMode.VerifyCA;
        }
    }

    private static void Write(string path, string pem)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, pem);
    }
}
