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

        var targetClientId = target.ClientId;
        var verificationScope = GetVerificationScope(target);
        var currentPublicKey = jwkGenerator.GetPublicKey(target.EncodedJwk);
        var originalSecretValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var updatedKeyVaultUrls = new List<string>();

        logger.LogInformation("Starting Maskinporten JWK rotation for client {ClientId}.", targetClientId);
        var originalJwks = await digdirMaskinportenAdminService.GetJwksAsync(targetClientId, cancellationToken);
        var generated = jwkGenerator.Generate(settings.NewKeyIdPrefix);

        var rotatedJwks = new MaskinportenJwkSet
        {
            Keys = [.. MergeDistinctKeys(originalJwks.Keys, currentPublicKey, generated.PublicJwk)]
        };

        var jwksUpdated = false;
        try
        {
            var updatedJwks = await digdirMaskinportenAdminService.UpdateJwksAsync(targetClientId, rotatedJwks, cancellationToken);
            jwksUpdated = true;

            logger.LogInformation(
                "Maskinporten JWKS updated for client {ClientId}. Returned kids: {Kids}.",
                targetClientId,
                FormatKids(updatedJwks.Keys));

            var verifiedJwks = await VerifyNewKeyAsync(
                settings,
                targetClientId,
                generated,
                verificationScope,
                target.Environment,
                cancellationToken);

            foreach (var keyVaultUrl in keyVaultUrls)
            {
                originalSecretValues[keyVaultUrl] = await keyVaultSecretStore.GetSecretValueAsync(
                    keyVaultUrl,
                    settings.KeyVaultSecretName,
                    cancellationToken);

                await keyVaultSecretStore.SetSecretAsync(
                    keyVaultUrl,
                    settings.KeyVaultSecretName,
                    generated.PrivateJwkBase64,
                    cancellationToken);

                updatedKeyVaultUrls.Add(keyVaultUrl);
            }

            logger.LogInformation(
                "Completed Maskinporten JWK rotation for client {ClientId}. New kid={Kid}. Keys before={Before}, keys after={After}. Key Vaults updated: {KeyVaultUrls}.",
                targetClientId,
                generated.Kid,
                originalJwks.Keys.Count,
                verifiedJwks.Keys.Count,
                string.Join(", ", keyVaultUrls));

            return new MaskinportenJwkRotationResult
            {
                TargetClientId = targetClientId,
                TargetClientName = "Correspondence",
                NewKid = generated.Kid,
                PreviousKeyCount = originalJwks.Keys.Count,
                CurrentKeyCount = verifiedJwks.Keys.Count,
                VerificationScope = verificationScope,
                KeyVaultSecretName = settings.KeyVaultSecretName
            };
        }
        catch (Exception ex)
        {
            List<Exception>? rollbackFailures = null;

            if (updatedKeyVaultUrls.Count > 0)
            {
                logger.LogWarning(
                    "Maskinporten JWK rotation failed after updating Key Vault secrets. Restoring previous secret values for vaults: {KeyVaultUrls}.",
                    string.Join(", ", updatedKeyVaultUrls));

                foreach (var keyVaultUrl in updatedKeyVaultUrls)
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
                            settings.KeyVaultSecretName,
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
                logger.LogWarning("Maskinporten JWK rotation failed after JWKS update. Restoring original JWKS for client {ClientId}.", targetClientId);
                try
                {
                    await digdirMaskinportenAdminService.UpdateJwksAsync(targetClientId, originalJwks, cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    rollbackFailures ??= [];
                    rollbackFailures.Add(rollbackEx);
                    logger.LogError(rollbackEx, "Failed to restore original JWKS for client {ClientId}.", targetClientId);
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

    private static IEnumerable<MaskinportenJwkKey> MergeDistinctKeys(
        IEnumerable<MaskinportenJwkKey> existingKeys,
        params MaskinportenJwkKey[] keys)
        => existingKeys
            .Concat(keys)
            .GroupBy(key => key.Kid, StringComparer.Ordinal)
            .Select(group => group.Last());

    private async Task<MaskinportenJwkSet> VerifyNewKeyAsync(
        MaskinportenJwkRotationSettings settings,
        string targetClientId,
        MaskinportenGeneratedJwk generated,
        string verificationScope,
        string environment,
        CancellationToken cancellationToken)
    {
        Exception? lastRetryableException = null;

        for (var attempt = 1; attempt <= settings.VerificationMaxAttempts; attempt++)
        {
            var currentJwks = await digdirMaskinportenAdminService.GetJwksAsync(targetClientId, cancellationToken);
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
                await tokenService.RequestTokenAsync(
                    targetClientId,
                    generated.PrivateJwkBase64,
                    verificationScope,
                    environment,
                    cancellationToken);

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
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

        if (GetKeyVaultUrls(settings).Count == 0)
        {
            throw new InvalidOperationException("Maskinporten JWK rotation Key Vault URL is missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.KeyVaultSecretName))
        {
            throw new InvalidOperationException("Maskinporten JWK rotation Key Vault secret name is missing.");
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
    }
}
