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
    IAltinnAuthorizationService altinnAuthorizationService) : IHandler<Guid, GetAttachmentDetailsResponse>
{
    private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>
    {
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".txt", "text/plain" }
    };

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

        var fileName = attachment.FileName ?? string.Empty;
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = MimeTypes.ContainsKey(fileExtension) ? MimeTypes[fileExtension] : "application/octet-stream";

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
            DataType = contentType,
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
