using System.Security.Claims;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class CanCorrespondenceBeForwardedHandler(
    ICorrespondenceRepository correspondenceRepository,
    ILogger<CanCorrespondenceBeForwardedHandler> logger
) : IHandler<Guid, CanCorrespondenceBeForwardedResponse>
{
    public async Task<OneOf<CanCorrespondenceBeForwardedResponse, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            logger.LogError("Forwarding check attempted without authenticated user context");
            return AuthorizationErrors.NoAccessToResource;
        }
        var doesCorrespondenceAllowForwarding = await correspondenceRepository.DoesCorrespondenceAllowForwarding(correspondenceId, cancellationToken);
        if (doesCorrespondenceAllowForwarding is null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", correspondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        return new CanCorrespondenceBeForwardedResponse { AllowForwarding = doesCorrespondenceAllowForwarding.Value };
    }
}