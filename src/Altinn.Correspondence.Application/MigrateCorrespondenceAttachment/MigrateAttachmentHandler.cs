using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using System.Transactions;

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

        AttachmentEntity? attachment = null;
        using (var transaction = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions()
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            Timeout = TimeSpan.FromSeconds(30)
        }, TransactionScopeAsyncFlowOption.Enabled))
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
                request.Attachment.StorageProvider = uploadResult.AsT0.StorageProviderEntity;
            }
            try
            {
                attachment = await attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);

                var attachmentStatus = new AttachmentStatusEntity()
                {
                    AttachmentId = request.Attachment.Id,
                    Status = AttachmentStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Published.ToString(),
                    PartyUuid = request.SenderPartyUuid
                };

                await attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, cancellationToken);
                transaction.Complete();

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
                var sqlState = e.InnerException?.Data["SqlState"]?.ToString();
                if (sqlState != "23505")
                {
                    throw;
                }
            }
        }

        // If we reach here, it means the attachment already exists in the database,
        // and we need to return the existing attachment information.
        attachment = await attachmentRepository.GetAttachmentByAltinn2Id(request.Attachment.Altinn2AttachmentId, cancellationToken);
        return new MigrateAttachmentResponse
        {
            AttachmentId = attachment.Id,
            ResourceId = attachment.ResourceId,
            Name = attachment.FileName,
            Checksum = attachment.Checksum,
            Status = AttachmentStatus.Published,
            StatusText = "Duplicate",
            StatusChanged = attachment.Created,
            DataLocationType = attachment.DataLocationType,
            SendersReference = attachment.SendersReference,
            FileName = attachment.FileName,
            DisplayName = attachment.DisplayName,
            Sender = attachment.Sender,
        };
    }
}
