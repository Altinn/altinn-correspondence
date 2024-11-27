using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler(
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    UserClaimsHelper userClaimsHelper) : IHandler<Guid, GetAttachmentDetailsResponse>
{

    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
        var attachmentStatus = attachment.GetLatestStatus();

        var response = new GetAttachmentDetailsResponse
        {
            ResourceId = attachment.ResourceId,
            AttachmentId = attachment.Id,
            Name = attachment.Name,
            Status = attachmentStatus.Status,
            Statuses = attachment.Statuses,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
            FileName = attachment.FileName,
            Sender = attachment.Sender,
            IsEncrypted = attachment.IsEncrypted,
            Checksum = attachment.Checksum,
        };
        return response;
    }
}
