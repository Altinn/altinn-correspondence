using System.Security.Cryptography.X509Certificates;

namespace Altinn.Correspondence.Common.Helpers;

public static class PemText
{
    public static string Normalize(string pem)
    {
        var value = pem.Trim().Trim('"');
        value = value.Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!value.EndsWith('\n'))
        {
            value += "\n";
        }

        return value;
    }

    public static X509Certificate2Collection LoadCertificates(string pem)
    {
        var normalized = Normalize(pem);
        var certificates = new X509Certificate2Collection();
        const string begin = "-----BEGIN CERTIFICATE-----";
        const string end = "-----END CERTIFICATE-----";
        var searchFrom = 0;
        while (searchFrom < normalized.Length)
        {
            var start = normalized.IndexOf(begin, searchFrom, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var stop = normalized.IndexOf(end, start, StringComparison.Ordinal);
            if (stop < 0)
            {
                break;
            }

            var blockEnd = stop + end.Length;
            certificates.Add(X509Certificate2.CreateFromPem(normalized[start..blockEnd]));
            searchFrom = blockEnd;
        }

        if (certificates.Count == 0)
        {
            throw new InvalidOperationException("PEM did not contain a certificate.");
        }

        return certificates;
    }
}
