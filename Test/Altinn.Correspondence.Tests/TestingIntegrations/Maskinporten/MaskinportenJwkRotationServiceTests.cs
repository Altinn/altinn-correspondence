using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Maskinporten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Maskinporten;

public class MaskinportenJwkRotationServiceTests
{
    [Fact]
    public async Task RotateAsync_PreservesExistingJwksAndWritesSecret()
    {
        var originalJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "stale-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-stale", E = "AQAB" }
            ]
        };
        var updatedJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "stale-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-stale", E = "AQAB" },
                new() { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            ]
        };

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalJwks)
            .ReturnsAsync(updatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync(
                "target-client",
                It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Select(key => key.Kid).OrderBy(kid => kid).SequenceEqual(new[] { "current-kid", "new-kid", "stale-kid" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(It.IsAny<string>()))
            .Returns(new MaskinportenJwkKey { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" });
        generator.Setup(service => service.Generate("rotation-prefix"))
            .Returns(new MaskinportenGeneratedJwk
            {
                Kid = "new-kid",
                PrivateJwkBase64 = "new-private-jwk",
                PublicJwk = new MaskinportenJwkKey { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            });

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("current-private-jwk");

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        var result = await service.RotateAsync(CancellationToken.None);

        Assert.Equal("new-kid", result.NewKid);
        Assert.Equal(2, result.PreviousKeyCount);
        Assert.Equal(3, result.CurrentKeyCount);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.VerifyAll();
    }

    [Fact]
    public async Task RotateAsync_WritesSecretToAllConfiguredKeyVaults()
    {
        var originalJwks = new MaskinportenJwkSet
        {
            Keys = [new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" }]
        };
        var updatedJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            ]
        };

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalJwks)
            .ReturnsAsync(updatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(It.IsAny<string>()))
            .Returns(new MaskinportenJwkKey { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" });
        generator.Setup(service => service.Generate(It.IsAny<string>()))
            .Returns(new MaskinportenGeneratedJwk
            {
                Kid = "new-kid",
                PrivateJwkBase64 = "new-private-jwk",
                PublicJwk = new MaskinportenJwkKey { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            });

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync(It.IsAny<string>(), "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("current-private-jwk");

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings => settings.AdditionalKeyVaultUrls = "https://kv-two.example, https://kv-three.example");

        await service.RotateAsync(CancellationToken.None);

        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv-two.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv-three.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_RestoresPreviouslyUpdatedKeyVaultSecretsWhenLaterVaultWriteFails()
    {
        var originalJwks = new MaskinportenJwkSet
        {
            Keys = [new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" }]
        };
        var updatedJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            ]
        };

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalJwks)
            .ReturnsAsync(updatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedJwks)
            .ReturnsAsync(originalJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(It.IsAny<string>()))
            .Returns(new MaskinportenJwkKey { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" });
        generator.Setup(service => service.Generate(It.IsAny<string>()))
            .Returns(new MaskinportenGeneratedJwk
            {
                Kid = "new-kid",
                PrivateJwkBase64 = "new-private-jwk",
                PublicJwk = new MaskinportenJwkKey { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            });

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("old-kv-one");
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv-two.example", "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("old-kv-two");
        keyVaultSecretStore.SetupSequence(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Returns(Task.CompletedTask);
        keyVaultSecretStore.Setup(store => store.SetSecretAsync("https://kv-two.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("key vault write failed"));

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings => settings.AdditionalKeyVaultUrls = "https://kv-two.example");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "old-kv-one", It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_RetriesVerificationUntilTokenSucceeds()
    {
        var originalJwks = new MaskinportenJwkSet
        {
            Keys = [new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" }]
        };
        var rotatedJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            ]
        };

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalJwks)
            .ReturnsAsync(rotatedJwks)
            .ReturnsAsync(rotatedJwks)
            .ReturnsAsync(rotatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rotatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(It.IsAny<string>()))
            .Returns(new MaskinportenJwkKey { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" });
        generator.Setup(service => service.Generate(It.IsAny<string>()))
            .Returns(new MaskinportenGeneratedJwk
            {
                Kid = "new-kid",
                PrivateJwkBase64 = "new-private-jwk",
                PublicJwk = new MaskinportenJwkKey { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            });

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.SetupSequence(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unknown key identifier (kid) for client."))
            .ThrowsAsync(new InvalidOperationException("Unknown key identifier (kid) for client."))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("current-private-jwk");

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings =>
            {
                settings.VerificationMaxAttempts = 3;
                settings.VerificationDelaySeconds = 0;
            });

        var result = await service.RotateAsync(CancellationToken.None);

        Assert.Equal("new-kid", result.NewKid);
        tokenService.Verify(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()), Times.Exactly(3));
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_RestoresOriginalJwksWhenVerificationKeepsFailing()
    {
        var originalJwks = new MaskinportenJwkSet
        {
            Keys = [new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" }]
        };
        var rotatedJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            ]
        };

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalJwks)
            .ReturnsAsync(rotatedJwks)
            .ReturnsAsync(rotatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rotatedJwks)
            .ReturnsAsync(originalJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(It.IsAny<string>()))
            .Returns(new MaskinportenJwkKey { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" });
        generator.Setup(service => service.Generate(It.IsAny<string>()))
            .Returns(new MaskinportenGeneratedJwk
            {
                Kid = "new-kid",
                PrivateJwkBase64 = "new-private-jwk",
                PublicJwk = new MaskinportenJwkKey { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            });

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unknown key identifier (kid) for client."));

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings =>
            {
                settings.VerificationMaxAttempts = 2;
                settings.VerificationDelaySeconds = 0;
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 2 && jwks.Keys.Any(key => key.Kid == "new-kid")),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_RestoresOriginalJwksWhenSecretWriteFails()
    {
        var originalJwks = new MaskinportenJwkSet
        {
            Keys = [new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" }]
        };
        var rotatedJwks = new MaskinportenJwkSet
        {
            Keys =
            [
                new() { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" },
                new() { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            ]
        };

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalJwks)
            .ReturnsAsync(rotatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rotatedJwks)
            .ReturnsAsync(originalJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(It.IsAny<string>()))
            .Returns(new MaskinportenJwkKey { Kid = "current-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-current", E = "AQAB" });
        generator.Setup(service => service.Generate(It.IsAny<string>()))
            .Returns(new MaskinportenGeneratedJwk
            {
                Kid = "new-kid",
                PrivateJwkBase64 = "new-private-jwk",
                PublicJwk = new MaskinportenJwkKey { Kid = "new-kid", Kty = "RSA", Use = "sig", Alg = "RS256", N = "n-new", E = "AQAB" }
            });

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("current-private-jwk");
        keyVaultSecretStore.Setup(store => store.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("key vault write failed"));

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 2 && jwks.Keys.Any(key => key.Kid == "new-kid")),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MaskinportenJwkRotationService CreateService(
        IDigdirMaskinportenAdminService digdirAdminService,
        IMaskinportenJwkGenerator generator,
        IMaskinportenTokenService tokenService,
        IKeyVaultSecretStore keyVaultSecretStore,
        Action<MaskinportenJwkRotationSettings>? configureRotationSettings = null)
    {
        var rotationSettingsValue = new MaskinportenJwkRotationSettings
        {
            AdminClientId = "admin-client",
            AdminEncodedJwk = CreateEncodedJwk("admin-kid"),
            KeyVaultUrl = "https://kv.example",
            AdditionalKeyVaultUrls = string.Empty,
            KeyVaultSecretName = "maskinporten-jwk",
            NewKeyIdPrefix = "rotation-prefix",
            VerificationMaxAttempts = 3,
            VerificationDelaySeconds = 0
        };
        configureRotationSettings?.Invoke(rotationSettingsValue);
        var rotationSettings = Options.Create(rotationSettingsValue);

        var targetSettings = Options.Create(new MaskinportenSettings
        {
            ClientId = "target-client",
            EncodedJwk = CreateEncodedJwk("current-kid"),
            Scope = "scope:a scope:b",
            Environment = "test"
        });

        return new MaskinportenJwkRotationService(
            rotationSettings,
            targetSettings,
            digdirAdminService,
            generator,
            tokenService,
            keyVaultSecretStore,
            NullLogger<MaskinportenJwkRotationService>.Instance);
    }

    private static string CreateEncodedJwk(string kid)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = kid,
            ["n"] = "n-value",
            ["e"] = "AQAB",
            ["d"] = "d-value",
            ["p"] = "p-value",
            ["q"] = "q-value",
            ["dp"] = "dp-value",
            ["dq"] = "dq-value",
            ["qi"] = "qi-value"
        });

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
