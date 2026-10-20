using Altinn.Correspondence.Common.Helpers;
using StackExchange.Redis;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Altinn.Correspondence.API.Helpers;

internal static class RedisConfiguration
{
    public static ConfigurationOptions Create(string connectionString, string? caPem)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        if (string.IsNullOrWhiteSpace(caPem))
        {
            return options;
        }

        var certificates = PemText.LoadCertificates(caPem);
        options.Ssl = true;
        options.CertificateValidation = (_, certificate, _, errors) => Validate(certificates, certificate, errors);
        return options;
    }

    internal static bool Validate(X509Certificate2Collection caCertificates, X509Certificate? certificate, SslPolicyErrors errors)
    {
        if (certificate is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return false;
        }

        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        using var serverCertificate = new X509Certificate2(certificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(caCertificates);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(serverCertificate);
    }
}
