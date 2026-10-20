using Altinn.Correspondence.API.Helpers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Storage;

public class RedisConfigurationTests
{
    [Fact]
    public void Create_EnablesSslWhenACaIsSupplied()
    {
        using var certificate = CreateCertificateAuthority();
        var options = RedisConfiguration.Create(
            "redis.internal:6379,user=correspondence,password=secret,abortConnect=false",
            certificate.ExportCertificatePem());

        Assert.True(options.Ssl);
        Assert.Equal("correspondence", options.User);
        Assert.NotNull(options.CertificateValidation);
    }

    [Fact]
    public void Validate_RejectsAHostNameMismatch()
    {
        using var certificate = CreateCertificateAuthority();
        var trusted = new X509Certificate2Collection { certificate };

        var accepted = RedisConfiguration.Validate(trusted, certificate, SslPolicyErrors.None);
        var rejected = RedisConfiguration.Validate(trusted, certificate, SslPolicyErrors.RemoteCertificateNameMismatch);

        Assert.True(accepted);
        Assert.False(rejected);
    }

    [Fact]
    public void Validate_TrustsACertificateSignedByTheSuppliedCa()
    {
        using var authority = CreateCertificateAuthority();
        using var server = CreateServerCertificate(authority);
        var trusted = new X509Certificate2Collection { authority };

        var accepted = RedisConfiguration.Validate(trusted, server, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(accepted);
    }

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=scaleway-test-ca", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateServerCertificate(X509Certificate2 authority)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=redis.internal", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);
        using var signed = request.Create(authority, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), serial);
        return signed.CopyWithPrivateKey(key);
    }
}
