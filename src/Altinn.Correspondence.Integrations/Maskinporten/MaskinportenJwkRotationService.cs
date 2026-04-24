using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.ExceptionServices;

namespace Altinn.Correspondence.Integrations.Maskinporten;

public class MaskinportenJwkRotationService(
    IOptions<MaskinportenJwkRotationSettings> rotationOptions,
    IOptions<MaskinportenSettings> targetOptions,
    IDigdirMaskinportenAdminService digdirMaskinportenAdminService,
    IMaskinportenJwkGenerator jwkGenerator,
    IMaskinportenTokenService tokenService,
    IKeyVaultSecretStore keyVaultSecretStore,
    ILogger<MaskinportenJwkRotationService> logger) : IMaskinportenJwkRotationService
{
    public async Task<MaskinportenJwkRotationResult> RotateAsync(CancellationToken cancellationToken)
    {
        var settings = rotationOptions.Value;
        var target = targetOptions.Value;
        ValidateConfiguration(settings, target);
        var keyVaultUrls = GetKeyVaultUrls(settings);
        await PreflightKeyVaultSecretsAsync(
            keyVaultUrls,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [settings.AdminKeyVaultSecretName] = null,
                [settings.KeyVaultSecretName] = null,
                [settings.AdminClientIdKeyVaultSecretName] = settings.AdminClientId,
                [settings.TargetClientIdKeyVaultSecretName] = target.ClientId
            },
            cancellationToken);

        var adminCredentials = CreateAdminCredentials(settings, target.Environment, settings.AdminEncodedJwk);
        var adminRotation = await RotateClientAsync(
            new RotationTarget(
                settings.AdminClientId,
                "Maskinporten admin",
                settings.AdminEncodedJwk,
                settings.AdminScope,
                settings.AdminKeyVaultSecretName,
                settings.AdminNewKeyIdPrefix),
            keyVaultUrls,
            adminCredentials,
            generated => VerifyAdminDcrAccessAsync(settings, target.Environment, generated, cancellationToken),
            cancellationToken);

        adminCredentials = CreateAdminCredentials(settings, target.Environment, adminRotation.Generated.PrivateJwkBase64);
        var targetRotation = await RotateClientAsync(
            new RotationTarget(
                target.ClientId,
                "Correspondence",
                target.EncodedJwk,
                GetVerificationScope(target),
                settings.KeyVaultSecretName,
                settings.NewKeyIdPrefix),
            keyVaultUrls,
            adminCredentials,
            generated => tokenService.RequestTokenAsync(
                target.ClientId,
                generated.PrivateJwkBase64,
                GetVerificationScope(target),
                target.Environment,
                cancellationToken),
            cancellationToken);

        return new MaskinportenJwkRotationResult
        {
            Clients = [adminRotation.Result, targetRotation.Result]
        };
    }

    private static IEnumerable<MaskinportenJwkKey> MergeDistinctKeys(
        IEnumerable<MaskinportenJwkKey> existingKeys,
        params MaskinportenJwkKey[] keys)
        => existingKeys
            .Concat(keys)
            .GroupBy(key => key.Kid, StringComparer.Ordinal)
            .Select(group => group.Last());

    private async Task<SingleRotationExecution> RotateClientAsync(
        RotationTarget rotationTarget,
        IReadOnlyList<string> keyVaultUrls,
        MaskinportenAdminApiCredentials adminCredentials,
        Func<MaskinportenGeneratedJwk, Task> verifyNewKey,
        CancellationToken cancellationToken)
    {
        var settings = rotationOptions.Value;
        var currentPublicKey = jwkGenerator.GetPublicKey(rotationTarget.CurrentEncodedJwk);
        var originalSecretValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var updatedKeyVaultUrls = new List<string>();

        logger.LogInformation("Starting Maskinporten JWK rotation for client {ClientId}.", rotationTarget.ClientId);
        var originalJwks = await digdirMaskinportenAdminService.GetJwksAsync(rotationTarget.ClientId, adminCredentials, cancellationToken);
        var generated = jwkGenerator.Generate(rotationTarget.NewKeyIdPrefix);

        var rotatedJwks = new MaskinportenJwkSet
        {
            Keys = [.. MergeDistinctKeys(originalJwks.Keys, currentPublicKey, generated.PublicJwk)]
        };

        var jwksUpdated = false;
        try
        {
            var updatedJwks = await digdirMaskinportenAdminService.UpdateJwksAsync(rotationTarget.ClientId, rotatedJwks, adminCredentials, cancellationToken);
            jwksUpdated = true;

            logger.LogInformation(
                "Maskinporten JWKS updated for client {ClientId}. Returned kids: {Kids}.",
                rotationTarget.ClientId,
                FormatKids(updatedJwks.Keys));

            var verifiedJwks = await VerifyNewKeyAsync(
                settings,
                rotationTarget.ClientId,
                generated,
                adminCredentials,
                verifyNewKey,
                cancellationToken);

            foreach (var keyVaultUrl in keyVaultUrls)
            {
                originalSecretValues[keyVaultUrl] = await keyVaultSecretStore.GetSecretValueAsync(
                    keyVaultUrl,
                    rotationTarget.KeyVaultSecretName,
                    cancellationToken);

                await keyVaultSecretStore.SetSecretAsync(
                    keyVaultUrl,
                    rotationTarget.KeyVaultSecretName,
                    generated.PrivateJwkBase64,
                    cancellationToken);

                updatedKeyVaultUrls.Add(keyVaultUrl);
            }

            logger.LogInformation(
                "Completed Maskinporten JWK rotation for client {ClientId}. New kid={Kid}. Keys before={Before}, keys after={After}. Key Vaults updated: {KeyVaultUrls}.",
                rotationTarget.ClientId,
                generated.Kid,
                originalJwks.Keys.Count,
                verifiedJwks.Keys.Count,
                string.Join(", ", keyVaultUrls));

            return new SingleRotationExecution(
                new MaskinportenJwkRotationClientResult
                {
                    ClientId = rotationTarget.ClientId,
                    ClientName = rotationTarget.ClientName,
                    NewKid = generated.Kid,
                    PreviousKeyCount = originalJwks.Keys.Count,
                    CurrentKeyCount = verifiedJwks.Keys.Count,
                    VerificationScope = rotationTarget.VerificationScope,
                    KeyVaultSecretName = rotationTarget.KeyVaultSecretName
                },
                generated);
        }
        catch (Exception ex)
        {
            List<Exception>? rollbackFailures = null;

            if (updatedKeyVaultUrls.Count > 0)
            {
                logger.LogWarning(
                    "Maskinporten JWK rotation failed after updating Key Vault secrets. Restoring previous secret values for vaults: {KeyVaultUrls}.",
                    string.Join(", ", updatedKeyVaultUrls));

                foreach (var keyVaultUrl in updatedKeyVaultUrls.AsEnumerable().Reverse())
                {
                    if (!originalSecretValues.TryGetValue(keyVaultUrl, out var originalSecretValue) || string.IsNullOrWhiteSpace(originalSecretValue))
                    {
                        var rollbackException = new InvalidOperationException(
                            $"Cannot restore Maskinporten JWK secret for vault {keyVaultUrl} because the original secret value was missing.");
                        rollbackFailures ??= [];
                        rollbackFailures.Add(rollbackException);
                        logger.LogError(rollbackException, "Failed to restore Maskinporten JWK secret for vault {KeyVaultUrl}.", keyVaultUrl);
                        continue;
                    }

                    try
                    {
                        await keyVaultSecretStore.SetSecretAsync(
                            keyVaultUrl,
                            rotationTarget.KeyVaultSecretName,
                            originalSecretValue,
                            cancellationToken);
                    }
                    catch (Exception rollbackEx)
                    {
                        rollbackFailures ??= [];
                        rollbackFailures.Add(rollbackEx);
                        logger.LogError(rollbackEx, "Failed to restore Maskinporten JWK secret for vault {KeyVaultUrl}.", keyVaultUrl);
                    }
                }
            }

            if (jwksUpdated)
            {
                logger.LogWarning("Maskinporten JWK rotation failed after JWKS update. Restoring original JWKS for client {ClientId}.", rotationTarget.ClientId);
                try
                {
                    await digdirMaskinportenAdminService.UpdateJwksAsync(rotationTarget.ClientId, originalJwks, adminCredentials, cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    rollbackFailures ??= [];
                    rollbackFailures.Add(rollbackEx);
                    logger.LogError(rollbackEx, "Failed to restore original JWKS for client {ClientId}.", rotationTarget.ClientId);
                }
            }

            if (rollbackFailures is not null)
            {
                rollbackFailures.Insert(0, ex);
                throw new AggregateException("Maskinporten JWK rotation failed and one or more rollback operations also failed.", rollbackFailures);
            }

            throw;
        }
    }

    private async Task<MaskinportenJwkSet> VerifyNewKeyAsync(
        MaskinportenJwkRotationSettings settings,
        string targetClientId,
        MaskinportenGeneratedJwk generated,
        MaskinportenAdminApiCredentials adminCredentials,
        Func<MaskinportenGeneratedJwk, Task> verifyNewKey,
        CancellationToken cancellationToken)
    {
        Exception? lastRetryableException = null;

        for (var attempt = 1; attempt <= settings.VerificationMaxAttempts; attempt++)
        {
            var currentJwks = await digdirMaskinportenAdminService.GetJwksAsync(targetClientId, adminCredentials, cancellationToken);
            var kidPresent = currentJwks.Keys.Any(key => string.Equals(key.Kid, generated.Kid, StringComparison.Ordinal));

            logger.LogInformation(
                "Verifying Maskinporten JWK rotation for client {ClientId}. Attempt {Attempt}/{MaxAttempts}. New kid present in JWKS: {KidPresent}. Current kids: {Kids}.",
                targetClientId,
                attempt,
                settings.VerificationMaxAttempts,
                kidPresent,
                FormatKids(currentJwks.Keys));

            try
            {
                await verifyNewKey(generated);

                logger.LogInformation(
                    "Verified Maskinporten JWK rotation for client {ClientId} with kid {Kid} on attempt {Attempt}/{MaxAttempts}.",
                    targetClientId,
                    generated.Kid,
                    attempt,
                    settings.VerificationMaxAttempts);

                return currentJwks;
            }
            catch (Exception ex) when (IsRetryableVerificationFailure(ex) && attempt < settings.VerificationMaxAttempts)
            {
                lastRetryableException = ex;

                logger.LogWarning(
                    ex,
                    "Verification attempt {Attempt}/{MaxAttempts} failed for client {ClientId} and will be retried in {DelaySeconds}s.",
                    attempt,
                    settings.VerificationMaxAttempts,
                    targetClientId,
                    settings.VerificationDelaySeconds);

                if (settings.VerificationDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(settings.VerificationDelaySeconds), cancellationToken);
                }
            }
        }

        if (lastRetryableException is not null)
        {
            ExceptionDispatchInfo.Capture(lastRetryableException).Throw();
        }

        throw new InvalidOperationException("Maskinporten JWK rotation verification failed without a retryable exception.");
    }

    private async Task VerifyAdminDcrAccessAsync(
        MaskinportenJwkRotationSettings settings,
        string environment,
        MaskinportenGeneratedJwk generated,
        CancellationToken cancellationToken)
    {
        var updatedAdminCredentials = CreateAdminCredentials(settings, environment, generated.PrivateJwkBase64);
        await digdirMaskinportenAdminService.GetJwksAsync(settings.AdminClientId, updatedAdminCredentials, cancellationToken);
    }

    private async Task PreflightKeyVaultSecretsAsync(
        IReadOnlyList<string> keyVaultUrls,
        IReadOnlyDictionary<string, string?> secretRequirements,
        CancellationToken cancellationToken)
    {
        foreach (var keyVaultUrl in keyVaultUrls)
        {
            foreach (var secretRequirement in secretRequirements)
            {
                var secretName = secretRequirement.Key;
                var secretValue = await keyVaultSecretStore.GetSecretValueAsync(keyVaultUrl, secretName, cancellationToken);

                if (secretRequirement.Value is not null
                    && !string.Equals(secretValue, secretRequirement.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Key Vault {keyVaultUrl} contains unexpected value for secret {secretName} during Maskinporten rotation preflight.");
                }
            }
        }
    }

    private static MaskinportenAdminApiCredentials CreateAdminCredentials(
        MaskinportenJwkRotationSettings settings,
        string environment,
        string encodedJwk)
        => new()
        {
            ClientId = settings.AdminClientId,
            EncodedJwk = encodedJwk,
            Scope = settings.AdminScope,
            ApiBaseUrl = settings.AdminApiBaseUrl,
            Environment = environment
        };

    private static string GetVerificationScope(MaskinportenSettings target)
    {
        var firstScope = target.Scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return firstScope
            ?? throw new InvalidOperationException("No verification scope configured for Maskinporten JWK rotation.");
    }

    private static IReadOnlyList<string> GetKeyVaultUrls(MaskinportenJwkRotationSettings settings)
        => new[] { settings.KeyVaultUrl }
            .Concat(
                settings.AdditionalKeyVaultUrls
                    .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(NormalizeKeyVaultUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeKeyVaultUrl(string url)
        => string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : url.Trim().TrimEnd('/');

    private static bool IsRetryableVerificationFailure(Exception exception)
        => exception is InvalidOperationException invalidOperationException
            && invalidOperationException.Message.Contains("Unknown key identifier (kid)", StringComparison.OrdinalIgnoreCase);

    private static string FormatKids(IEnumerable<MaskinportenJwkKey> keys)
    {
        var kids = keys
            .Select(key => string.IsNullOrWhiteSpace(key.Kid) ? "<missing>" : key.Kid)
            .ToArray();

        return kids.Length == 0 ? "<none>" : string.Join(", ", kids);
    }

    private static void ValidateConfiguration(MaskinportenJwkRotationSettings settings, MaskinportenSettings target)
    {
        if (string.IsNullOrWhiteSpace(settings.AdminClientId))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin client id is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminEncodedJwk))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin JWK is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminScope))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin scope is missing.");
        }

        if (GetKeyVaultUrls(settings).Count == 0)
        {
            throw new InvalidOperationException("Maskinporten JWK rotation Key Vault URL is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminKeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminClientIdKeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin client id Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.KeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.TargetClientIdKeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation target client id Key Vault secret name is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.AdminNewKeyIdPrefix))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation admin key id prefix is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.NewKeyIdPrefix))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation key id prefix is missing.");
        }

        if (settings.VerificationMaxAttempts <= 0)
        {
            throw new InvalidOperationException("Maskinporten JWK rotation verification max attempts must be greater than zero.");
        }

        if (settings.VerificationDelaySeconds < 0)
        {
            throw new InvalidOperationException("Maskinporten JWK rotation verification delay seconds cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(target.ClientId))
        {
            throw new InvalidOperationException("Target Maskinporten client id is missing.");
        }

        if (string.IsNullOrWhiteSpace(target.EncodedJwk))
        {
            throw new InvalidOperationException("Target Maskinporten private JWK is missing.");
        }

        if (string.IsNullOrWhiteSpace(target.Scope))
        {
            throw new InvalidOperationException("Target Maskinporten scope is missing.");
        }

        if (string.IsNullOrWhiteSpace(target.Environment))
        {
            throw new InvalidOperationException("Target Maskinporten environment is missing.");
        }
    }

    private sealed record RotationTarget(
        string ClientId,
        string ClientName,
        string CurrentEncodedJwk,
        string VerificationScope,
        string KeyVaultSecretName,
        string NewKeyIdPrefix);

    private sealed record SingleRotationExecution(
        MaskinportenJwkRotationClientResult Result,
        MaskinportenGeneratedJwk Generated);
}
