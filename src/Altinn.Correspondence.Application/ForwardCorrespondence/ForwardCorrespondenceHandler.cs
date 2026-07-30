using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class ForwardCorrespondenceHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnNotificationService altinnNotificationService,
    ICorrespondenceRepository correspondenceRepository,
    ComposedEmailHelper composedEmailHelper,
    ILogger<ForwardCorrespondenceHandler> logger
) : IHandler<ForwardCorrespondenceRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(ForwardCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken, true);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (!correspondence.AllowForwarding)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} does not allow forwarding", request.CorrespondenceId);
            return CorrespondenceErrors.ForwardingNotAllowed;
        }
        var userHasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken);
        if (!userHasAccess)
        {
            logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }

        logger.LogInformation("Forwarding correspondence {CorrespondenceId} to {ForwardTo}", request.CorrespondenceId, request.ForwardTo.SanitizeForLogging());
        var composedEmailRequest = await composedEmailHelper.MapToComposedEmailRequest(correspondence, request.ForwardTo, cancellationToken);
        var composedEmailResponse = await altinnNotificationService.CreateComposedEmail(composedEmailRequest, cancellationToken);
        if (composedEmailResponse == null)
        {
            logger.LogError("Failed to create composed email for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return CorrespondenceErrors.ForwardingNotAllowed;
        }
        return composedEmailResponse.NotificationOrderId;
    }
}