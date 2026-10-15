using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class CanCorrespondenceBeForwardedHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAttachmentRepository attachmentRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
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
        var allowForwarding = await correspondenceRepository.DoesCorrespondenceAllowForwarding(correspondenceId, cancellationToken);
        if (allowForwarding is null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", correspondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (!allowForwarding.Value)
        {
            return new CanCorrespondenceBeForwardedResponse
            {
                AllowForwarding = false,
                ForwardingConstraints = ForwardingConstraints.None
            };
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", correspondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        return new CanCorrespondenceBeForwardedResponse
        {
            AllowForwarding = true,
            ForwardingConstraints = await GetForwardingConstraints(correspondence, user, cancellationToken)
        };
    }

    private async Task<ForwardingConstraints> GetForwardingConstraints(CorrespondenceEntity correspondence, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var inaccessibleAttachment = await altinnAuthorizationService.GetFirstInaccessibleAttachment(user, correspondence, cancellationToken);
        if (inaccessibleAttachment is not null)
        {
            logger.LogInformation("User lacks access to attachment resource {ResourceId} on correspondence {CorrespondenceId}", inaccessibleAttachment.ResourceId, correspondence.Id);
            return ForwardingConstraints.NotAccessToAllAttachments;
        }
        var totalAttachmentSize = await attachmentRepository.GetTotalAttachmentSizeByCorrespondence(correspondence.Id, cancellationToken);
        return totalAttachmentSize > 10_000_000 // 10 MB
            ? ForwardingConstraints.AttachmentsExceedMaxLimit
            : ForwardingConstraints.None;
    }
}
