using Altinn.Correspondence.Application.SendSlackNotification;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.MaskinportenJwkRotation;

public class MaskinportenJwkRotationHandler(
    IMaskinportenJwkRotationService rotationService,
    SendSlackNotificationHandler slackNotificationHandler,
    ILogger<MaskinportenJwkRotationHandler> logger)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task Process(CancellationToken cancellationToken)
    {
        try
        {
            var result = await rotationService.RotateAsync(cancellationToken);
            await slackNotificationHandler.Process(
                "Maskinporten JWK rotation completed",
                $"Client {result.TargetClientName} ({result.TargetClientId}) rotated to kid {result.NewKid}. Keys before: {result.PreviousKeyCount}, keys after: {result.CurrentKeyCount}. Secret updated: {result.KeyVaultSecretName}. Verification scope: {result.VerificationScope}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Maskinporten JWK rotation failed.");
            await slackNotificationHandler.Process(
                "Maskinporten JWK rotation failed",
                ex.Message);
            throw;
        }
    }
}
