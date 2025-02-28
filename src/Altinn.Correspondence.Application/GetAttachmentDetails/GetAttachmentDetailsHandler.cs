using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler(
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService) : IHandler<Guid, GetAttachmentDetailsResponse>
{

    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        var correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
        var attachmentStatus = attachment.GetLatestStatus();

        var fileName = attachment.FileName;
        var contentType = FileConstants.GetMIMEType(fileName);

        var response = new GetAttachmentDetailsResponse
        {
            ResourceId = attachment.ResourceId,
            AttachmentId = attachment.Id,
            Status = attachmentStatus.Status,
            Statuses = attachment.Statuses,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = contentType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
            FileName = attachment.FileName,
            DisplayName = attachment.DisplayName,
            Sender = attachment.Sender,
            IsEncrypted = attachment.IsEncrypted,
            Checksum = attachment.Checksum,
        };
        return response;
    }
}
