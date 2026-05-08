using Altinn.Correspondence.Integrations.Maskinporten;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Maskinporten;

public class MaskinportenTokenServiceTests
{
    [Fact]
    public async Task RequestTokenAsync_CreatesSignedAssertionWithoutDisposedRsa()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler()) { BaseAddress = new Uri("https://test.maskinporten.no/") });

        var service = new MaskinportenTokenService(httpClientFactory.Object);

        var token = await service.RequestTokenAsync(
            "client-id",
            CreateEncodedJwk(),
            "scope:a",
            "test",
            CancellationToken.None);

        Assert.Equal("access-token", token);
    }

    private static string CreateEncodedJwk()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(true);

        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = "kid-1",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!),
            ["d"] = Base64UrlEncoder.Encode(parameters.D!),
            ["p"] = Base64UrlEncoder.Encode(parameters.P!),
            ["q"] = Base64UrlEncoder.Encode(parameters.Q!),
            ["dp"] = Base64UrlEncoder.Encode(parameters.DP!),
            ["dq"] = Base64UrlEncoder.Encode(parameters.DQ!),
            ["qi"] = Base64UrlEncoder.Encode(parameters.InverseQ!)
        });

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"access-token\"}", Encoding.UTF8, "application/json")
            });
    }
}
