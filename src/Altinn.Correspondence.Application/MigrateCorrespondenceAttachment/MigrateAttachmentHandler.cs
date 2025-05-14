using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;

public class MigrateAttachmentHandler(
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    MigrateAttachmentHelper attachmentHelper,
    ILogger<MigrateAttachmentHandler> logger) : IHandler<MigrateAttachmentRequest, MigrateAttachmentResponse>
{
    public async Task<OneOf<MigrateAttachmentResponse, Error>> Process(MigrateAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (request.ContentLength == 0)
        {
            return AttachmentErrors.InvalidFileSize;
        }

        return await TransactionWithRetriesPolicy.Execute<MigrateAttachmentResponse>(async (cancellationToken) =>
        {
            var uploadResult = await attachmentHelper.UploadAttachment(request, request.SenderPartyUuid, cancellationToken);
            if (uploadResult.IsT1)
            {
                return AttachmentErrors.UploadFailed;
            }
            else
            {
                request.Attachment.DataLocationUrl = uploadResult.AsT0.DataLocationUrl;
                request.Attachment.Checksum = uploadResult.AsT0.Checksum;
                request.Attachment.AttachmentSize = uploadResult.AsT0.Size;
            }
            try
            {
                var attachment = await attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);

                var attachmentStatus = new AttachmentStatusEntity()
                {
                    AttachmentId = request.Attachment.Id,
                    Status = AttachmentStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Published.ToString(),
                    PartyUuid = request.SenderPartyUuid
                };

                await attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, cancellationToken);

                return new MigrateAttachmentResponse
                {
                    AttachmentId = attachment.Id,
                    ResourceId = attachment.ResourceId,
                    Name = attachment.FileName,
                    Checksum = attachment.Checksum,
                    Status = attachmentStatus.Status,
                    StatusText = attachmentStatus.StatusText,
                    StatusChanged = attachmentStatus.StatusChanged,
                    DataLocationType = attachment.DataLocationType,
                    SendersReference = attachment.SendersReference,
                    FileName = attachment.FileName,
                    DisplayName = attachment.DisplayName,
                    Sender = attachment.Sender,
                };
            } 
            catch (DbUpdateException e)
            {
                if (e.InnerException?.Data.Contains("SqlState") ?? false)
                {
                    var sqlState = e.InnerException.Data["SqlState"].ToString();
                    if (sqlState == "23505")
                    {
                        return AttachmentErrors.AttachmentAlreadyMigrated;
                    }
                }
                throw e;
            }
        }, logger, cancellationToken);
    }
}
