using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class CanCorrespondenceBeForwardedHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAttachmentRepository attachmentRepository,
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
        return new CanCorrespondenceBeForwardedResponse
        {
            AllowForwarding = allowForwarding.Value,
            ForwardingConstraints = allowForwarding.Value
                ? await GetForwardingConstraints(correspondenceId, cancellationToken)
                : ForwardingConstraints.None
        };
    }

    private async Task<ForwardingConstraints> GetForwardingConstraints(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var totalAttachmentSize = await attachmentRepository.GetTotalAttachmentSizeByCorrespondence(correspondenceId, cancellationToken);
        return totalAttachmentSize > 10_000_000 // 10 MB
            ? ForwardingConstraints.AttachmentsExceedMaxLimit
            : ForwardingConstraints.None;
    }
}