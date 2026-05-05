using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Maskinporten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Maskinporten;

public class MaskinportenJwkRotationServiceTests
{
    [Fact]
    public async Task RotateAsync_RotatesAdminThenCorrespondenceSuccessfully()
    {
        var adminOriginalJwks = CreateJwks("admin-kid", "admin-stale");
        var adminUpdatedJwks = CreateJwks("admin-kid", "admin-stale", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid", "stale-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "stale-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync(
                "admin-client",
                It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Select(key => key.Kid).OrderBy(kid => kid).SequenceEqual(new[] { "admin-kid", "admin-stale", "new-admin-kid" })),
                It.Is<MaskinportenAdminApiCredentials>(credentials => credentials.EncodedJwk == CreateEncodedJwk("admin-kid")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync(
                "target-client",
                It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Select(key => key.Kid).OrderBy(kid => kid).SequenceEqual(new[] { "current-kid", "new-target-kid", "stale-kid" })),
                It.Is<MaskinportenAdminApiCredentials>(credentials => credentials.EncodedJwk == "new-admin-private-jwk"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);
        var containerAppRefreshService = new Mock<IContainerAppRefreshService>();
        containerAppRefreshService
            .Setup(service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings => settings.RefreshContainerAppsAfterRotation = true,
            containerAppRefreshService: containerAppRefreshService.Object);

        var result = await service.RotateAsync(CancellationToken.None);

        Assert.Collection(
            result.Clients,
            adminResult =>
            {
                Assert.Equal("admin-client", adminResult.ClientId);
                Assert.Equal("new-admin-kid", adminResult.NewKid);
                Assert.Equal("maskinporten-admin-jwk", adminResult.KeyVaultSecretName);
            },
            targetResult =>
            {
                Assert.Equal("target-client", targetResult.ClientId);
                Assert.Equal("new-target-kid", targetResult.NewKid);
                Assert.Equal("maskinporten-jwk", targetResult.KeyVaultSecretName);
            });
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "new-admin-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        containerAppRefreshService.Verify(
            service => service.RefreshAsync("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.App/containerApps/test-app", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RotateAsync_ThrowsBeforeAdminRotationWhenTargetScopeIsMissing()
    {
        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        var generator = new Mock<IMaskinportenJwkGenerator>();
        var tokenService = new Mock<IMaskinportenTokenService>();
        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            target: target => target.Scope = "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.GetJwksAsync(It.IsAny<string>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()), Times.Never);
        keyVaultSecretStore.Verify(store => store.GetSecretValueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_UsesNewAdminKeyForAdminVerificationAndCorrespondenceRotation()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        await service.RotateAsync(CancellationToken.None);

        digdirAdminService.Verify(service => service.GetJwksAsync(
            "admin-client",
            It.Is<MaskinportenAdminApiCredentials>(credentials => credentials.EncodedJwk == "new-admin-private-jwk"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.IsAny<MaskinportenJwkSet>(),
            It.Is<MaskinportenAdminApiCredentials>(credentials => credentials.EncodedJwk == "new-admin-private-jwk"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_RotatesClientOnceAndWritesGeneratedJwkToConfiguredTargetVault()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings => settings.Targets =
            [
                new MaskinportenJwkRotationTarget
                {
                    Name = "at22",
                    KeyVaultUrl = "https://at22-kv.example/"
                }
            ]);

        var result = await service.RotateAsync(CancellationToken.None);

        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "new-admin-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://at22-kv.example", "maskinporten-admin-jwk", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://at22-kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(result.Clients, client => client.ClientName == "Correspondence" && client.ClientId == "target-client");
    }

    [Fact]
    public async Task RotateAsync_ThrowsBeforeRotationWhenVaultClientIdsDoNotMatch()
    {
        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        var generator = new Mock<IMaskinportenJwkGenerator>();
        var tokenService = new Mock<IMaskinportenTokenService>();
        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-admin-client-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync("admin-client");
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-client-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync("target-client");
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-admin-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("maskinporten-admin-jwk-old");
        keyVaultSecretStore.Setup(store => store.GetSecretValueAsync("https://kv.example", "maskinporten-jwk", It.IsAny<CancellationToken>()))
            .ReturnsAsync("maskinporten-jwk-old");

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            target: target => target.ClientId = "different-target-client");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.GetJwksAsync(It.IsAny<string>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()), Times.Never);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_RestoresAdminRotationWhenAdminSecretWriteFails()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminOriginalJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);
        keyVaultSecretStore.Setup(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "new-admin-private-jwk", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("key vault write failed"));

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "new-admin-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "maskinporten-admin-jwk-old", It.IsAny<CancellationToken>()), Times.Never);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "admin-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "admin-kid"),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_VerifiesAdminWithNewKeyBeforeCorrespondenceStarts()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");
        var callOrder = new List<string>();

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.Setup(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, MaskinportenAdminApiCredentials credentials, CancellationToken _) =>
            {
                callOrder.Add(credentials.EncodedJwk == "new-admin-private-jwk" ? "admin-verify" : "admin-get");
                return callOrder.Count == 1 ? adminOriginalJwks : adminUpdatedJwks;
            });
        digdirAdminService.Setup(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, MaskinportenAdminApiCredentials _, CancellationToken _) =>
            {
                callOrder.Add("target-get");
                return callOrder.Count(c => c == "target-get") == 1 ? targetOriginalJwks : targetUpdatedJwks;
            });
        digdirAdminService.Setup(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("admin-update"))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("target-update"))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        await service.RotateAsync(CancellationToken.None);

        Assert.True(callOrder.IndexOf("admin-verify") < callOrder.IndexOf("target-update"));
    }

    [Fact]
    public async Task RotateAsync_RetriesAdminVerificationWhenPollingJwksSeesPropagationDelay()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");
        var adminGetAttempts = 0;

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.Setup(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, MaskinportenAdminApiCredentials credentials, CancellationToken _) =>
            {
                if (credentials.EncodedJwk == CreateEncodedJwk("admin-kid"))
                {
                    adminGetAttempts++;
                    if (adminGetAttempts == 1)
                    {
                        return adminOriginalJwks;
                    }

                    if (adminGetAttempts == 2)
                    {
                        throw new InvalidOperationException("Unknown key identifier (kid) for client.");
                    }
                }

                return adminUpdatedJwks;
            });
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        await service.RotateAsync(CancellationToken.None);

        Assert.True(adminGetAttempts >= 3);
        digdirAdminService.Verify(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_RetriesTargetVerificationWhenTokenEndpointSeesPropagationDelay()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.Setup(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.SetupSequence(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unknown key identifier (kid) for client."))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object);

        await service.RotateAsync(CancellationToken.None);

        tokenService.Verify(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()), Times.Exactly(2));
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_DoesNotRollbackAdminWhenCorrespondenceFails()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks)
            .ReturnsAsync(targetOriginalJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unknown key identifier (kid) for client."));

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings =>
            {
                settings.VerificationMaxAttempts = 1;
                settings.VerificationDelaySeconds = 0;
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "admin-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Any(key => key.Kid == "new-admin-kid")),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "admin-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "admin-kid"),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Never);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Any(key => key.Kid == "new-target-kid")),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "new-admin-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-admin-jwk", "maskinporten-admin-jwk-old", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_RestoresWrittenTargetVaultsWhenAdditionalVaultWriteFails()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks)
            .ReturnsAsync(targetOriginalJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);
        keyVaultSecretStore.SetupSequence(store => store.SetSecretAsync("https://at22-kv.example", "maskinporten-jwk", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("key vault write failed"));

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings => settings.Targets =
            [
                new MaskinportenJwkRotationTarget
                {
                    Name = "at22",
                    KeyVaultUrl = "https://at22-kv.example"
                }
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Any(key => key.Kid == "new-target-kid")),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "admin-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "admin-kid"),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Never);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "maskinporten-jwk-old", It.IsAny<CancellationToken>()), Times.Once);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://at22-kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_KeepsUpdatedJwksWhenKeyVaultRollbackFails()
    {
        var adminOriginalJwks = CreateJwks("admin-kid");
        var adminUpdatedJwks = CreateJwks("admin-kid", "new-admin-kid");
        var targetOriginalJwks = CreateJwks("current-kid");
        var targetUpdatedJwks = CreateJwks("current-kid", "new-target-kid");

        var digdirAdminService = new Mock<IDigdirMaskinportenAdminService>();
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("admin-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminOriginalJwks)
            .ReturnsAsync(adminUpdatedJwks)
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.GetJwksAsync("target-client", It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOriginalJwks)
            .ReturnsAsync(targetUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("admin-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUpdatedJwks);
        digdirAdminService.SetupSequence(service => service.UpdateJwksAsync("target-client", It.IsAny<MaskinportenJwkSet>(), It.IsAny<MaskinportenAdminApiCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUpdatedJwks);

        var generator = new Mock<IMaskinportenJwkGenerator>();
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("admin-kid")))
            .Returns(CreatePublicKey("admin-kid"));
        generator.Setup(service => service.GetPublicKey(CreateEncodedJwk("current-kid")))
            .Returns(CreatePublicKey("current-kid"));
        generator.SetupSequence(service => service.Generate(It.IsAny<string>()))
            .Returns(CreateGeneratedJwk("new-admin-kid", "new-admin-private-jwk"))
            .Returns(CreateGeneratedJwk("new-target-kid", "new-target-private-jwk"));

        var tokenService = new Mock<IMaskinportenTokenService>();
        tokenService.Setup(service => service.RequestTokenAsync("target-client", "new-target-private-jwk", "scope:a", "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");

        var keyVaultSecretStore = new Mock<IKeyVaultSecretStore>();
        SetupAllSecretReads(keyVaultSecretStore);
        keyVaultSecretStore.Setup(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        keyVaultSecretStore.Setup(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "maskinporten-jwk-old", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("key vault rollback failed"));
        keyVaultSecretStore.Setup(store => store.SetSecretAsync("https://at22-kv.example", "maskinporten-jwk", "new-target-private-jwk", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("key vault write failed"));

        var service = CreateService(
            digdirAdminService.Object,
            generator.Object,
            tokenService.Object,
            keyVaultSecretStore.Object,
            settings => settings.Targets =
            [
                new MaskinportenJwkRotationTarget
                {
                    Name = "at22",
                    KeyVaultUrl = "https://at22-kv.example"
                }
            ]);

        await Assert.ThrowsAsync<AggregateException>(() => service.RotateAsync(CancellationToken.None));

        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Any(key => key.Kid == "new-target-kid")),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Once);
        digdirAdminService.Verify(service => service.UpdateJwksAsync(
            "target-client",
            It.Is<MaskinportenJwkSet>(jwks => jwks.Keys.Count == 1 && jwks.Keys[0].Kid == "current-kid"),
            It.IsAny<MaskinportenAdminApiCredentials>(),
            It.IsAny<CancellationToken>()), Times.Never);
        keyVaultSecretStore.Verify(store => store.SetSecretAsync("https://kv.example", "maskinporten-jwk", "maskinporten-jwk-old", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MaskinportenJwkRotationSettings_BindsExplicitTargetsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaskinportenJwkRotationSettings:Enabled"] = "true",
                ["MaskinportenJwkRotationSettings:RefreshContainerAppsAfterRotation"] = "true",
                ["MaskinportenJwkRotationSettings:ContainerAppResourceId"] = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.App/containerApps/test-app",
                ["MaskinportenJwkRotationSettings:Targets:0:Name"] = "at22",
                ["MaskinportenJwkRotationSettings:Targets:0:KeyVaultUrl"] = "https://at22-kv.example/",
                ["MaskinportenJwkRotationSettings:Targets:0:ContainerAppResourceId"] = "/subscriptions/sub/resourceGroups/at22-rg/providers/Microsoft.App/containerApps/at22-app"
            })
            .Build();

        var settings = configuration.GetSection(nameof(MaskinportenJwkRotationSettings)).Get<MaskinportenJwkRotationSettings>();

        Assert.NotNull(settings);
        Assert.True(settings.Enabled);
        Assert.True(settings.RefreshContainerAppsAfterRotation);
        Assert.Equal("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.App/containerApps/test-app", settings.ContainerAppResourceId);
        var target = Assert.Single(settings.Targets);
        Assert.Equal("at22", target.Name);
        Assert.Equal("https://at22-kv.example/", target.KeyVaultUrl);
        Assert.Equal("/subscriptions/sub/resourceGroups/at22-rg/providers/Microsoft.App/containerApps/at22-app", target.ContainerAppResourceId);
    }

    private static MaskinportenJwkRotationService CreateService(
        IDigdirMaskinportenAdminService digdirAdminService,
        IMaskinportenJwkGenerator generator,
        IMaskinportenTokenService tokenService,
        IKeyVaultSecretStore keyVaultSecretStore,
        Action<MaskinportenJwkRotationSettings>? configureRotationSettings = null,
        Action<MaskinportenSettings>? target = null,
        IContainerAppRefreshService? containerAppRefreshService = null)
    {
        var rotationSettingsValue = new MaskinportenJwkRotationSettings
        {
            AdminClientId = "admin-client",
            AdminEncodedJwk = CreateEncodedJwk("admin-kid"),
            AdminKeyVaultSecretName = "maskinporten-admin-jwk",
            AdminClientIdKeyVaultSecretName = "maskinporten-admin-client-id",
            AdminNewKeyIdPrefix = "admin-rotation-prefix",
            KeyVaultUrl = "https://kv.example",
            ContainerAppResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.App/containerApps/test-app",
            RefreshContainerAppsAfterRotation = false,
            KeyVaultSecretName = "maskinporten-jwk",
            TargetClientIdKeyVaultSecretName = "maskinporten-client-id",
            NewKeyIdPrefix = "target-rotation-prefix",
            VerificationMaxAttempts = 3,
            VerificationDelaySeconds = 0
        };
        configureRotationSettings?.Invoke(rotationSettingsValue);
        var rotationSettings = Options.Create(rotationSettingsValue);

        var targetSettingsValue = new MaskinportenSettings
        {
            ClientId = "target-client",
            EncodedJwk = CreateEncodedJwk("current-kid"),
            Scope = "scope:a scope:b",
            Environment = "test"
        };
        target?.Invoke(targetSettingsValue);
        var targetSettings = Options.Create(targetSettingsValue);

        return new MaskinportenJwkRotationService(
            rotationSettings,
            targetSettings,
            digdirAdminService,
            generator,
            tokenService,
            keyVaultSecretStore,
            containerAppRefreshService ?? Mock.Of<IContainerAppRefreshService>(),
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

    private static void SetupAllSecretReads(Mock<IKeyVaultSecretStore> keyVaultSecretStore)
        => keyVaultSecretStore
            .Setup(store => store.GetSecretValueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string secretName, CancellationToken _) => secretName switch
            {
                "maskinporten-admin-client-id" => "admin-client",
                "maskinporten-client-id" => "target-client",
                _ => $"{secretName}-old"
            });

    private static MaskinportenJwkSet CreateJwks(params string[] kids)
        => new()
        {
            Keys = kids.Select(CreatePublicKey).ToList()
        };

    private static MaskinportenGeneratedJwk CreateGeneratedJwk(string kid, string privateJwk)
        => new()
        {
            Kid = kid,
            PrivateJwkBase64 = privateJwk,
            PublicJwk = CreatePublicKey(kid)
        };

    private static MaskinportenJwkKey CreatePublicKey(string kid)
        => new()
        {
            Kid = kid,
            Kty = "RSA",
            Use = "sig",
            Alg = "RS256",
            N = $"n-{kid}",
            E = "AQAB"
        };
}
