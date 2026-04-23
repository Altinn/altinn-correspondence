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
            var summary = string.Join(
                " ",
                result.Clients.Select(client =>
                    $"Client {client.ClientName} ({client.ClientId}) rotated to kid {client.NewKid}. Keys before: {client.PreviousKeyCount}, keys after: {client.CurrentKeyCount}. Secret updated: {client.KeyVaultSecretName}. Verification scope: {client.VerificationScope}."));
            await slackNotificationHandler.Process(
                "Maskinporten JWK rotation completed",
                summary);
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
