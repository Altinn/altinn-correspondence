using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Hosting;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class MigrateUploadAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, UploadHelper uploadHelper) : IHandler<UploadAttachmentRequest, MigrateUploadAttachmentResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly UploadHelper _uploadHelper = uploadHelper;

    public async Task<OneOf<MigrateUploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckMigrationAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var maxUploadSize = long.Parse(int.MaxValue.ToString());
        if (request.ContentLength > maxUploadSize || request.ContentLength == 0)
        {
            return Errors.InvalidFileSize;
        }
        if (attachment.StatusHasBeen(AttachmentStatus.UploadProcessing))
        {
            return Errors.InvalidUploadAttachmentStatus;
        }
        var uploadResult = await _uploadHelper.UploadAttachment(request.UploadStream, request.AttachmentId, cancellationToken);

        if (uploadResult.IsT1)
        {
            return Errors.UploadFailed;
        }
        var savedAttachment = await _attachmentRepository.GetAttachmentById(uploadResult.AsT0.AttachmentId, true, cancellationToken);
        if (savedAttachment == null)
        {
            return Errors.UploadFailed;
        }

        var attachmentStatus = savedAttachment.GetLatestStatus();
        return new MigrateUploadAttachmentResponse
        {
            AttachmentId = attachment.Id,
            ResourceId = attachment.ResourceId,
            Name = attachment.Name,
            Checksum = attachment.Checksum,
            Status = attachmentStatus.Status,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = attachment.DataType,
            SendersReference = attachment.SendersReference,
            FileName = attachment.FileName,
            Sender = attachment.Sender,
        };
    }
}
