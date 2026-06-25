using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
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
    ApplicationDbContext dbContext,
    ILogger<MigrateAttachmentHandler> logger) : IHandler<MigrateAttachmentRequest, MigrateAttachmentResponse>
{
    public async Task<OneOf<MigrateAttachmentResponse, Error>> Process(MigrateAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (request.ContentLength == 0)
        {
            return AttachmentErrors.InvalidFileSize("2GB");
        }

        var uploadResult = await attachmentHelper.UploadAttachment(request, request.SenderPartyUuid, cancellationToken);
        if (uploadResult.IsT1)
        {
            return AttachmentErrors.UploadFailed;
        }

        request.Attachment.DataLocationUrl = uploadResult.AsT0.DataLocationUrl;
        request.Attachment.Checksum = uploadResult.AsT0.Checksum;
        request.Attachment.AttachmentSize = uploadResult.AsT0.Size;
        request.Attachment.StorageProvider = uploadResult.AsT0.StorageProviderEntity;

        var attempt = await DatabaseTransactionHelper.ExecuteWithRetryAsync(dbContext, async ct =>
        {
            using var transaction = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions()
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromSeconds(30)
            }, TransactionScopeAsyncFlowOption.Enabled);

            try
            {
                var attachment = await attachmentRepository.InitializeAttachment(request.Attachment, ct);

                var attachmentStatus = new AttachmentStatusEntity()
                {
                    AttachmentId = request.Attachment.Id,
                    Status = AttachmentStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Published.ToString(),
                    PartyUuid = request.SenderPartyUuid
                };

                await attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, ct);
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                    transaction.Complete();
                }
                catch (DbUpdateException e) when (e.IsPostgresUniqueViolation())
                {
                    return MigrateAttachmentAttempt.Duplicate();
                }

                return MigrateAttachmentAttempt.Created(new MigrateAttachmentResponse
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
                });
            }
            catch (Exception)
            {
                throw;
            }
        }, cancellationToken);

        return attempt switch
        {
            MigrateAttachmentAttempt.CreatedAttempt created => created.Response,
            MigrateAttachmentAttempt.DuplicateAttempt => await BuildDuplicateResponse(request, cancellationToken),
        };
    }

    private async Task<MigrateAttachmentResponse> BuildDuplicateResponse(MigrateAttachmentRequest request, CancellationToken cancellationToken)
    {
        var existingAttachment = await attachmentRepository.GetAttachmentByAltinn2Id(request.Attachment.Altinn2AttachmentId, cancellationToken);
        return new MigrateAttachmentResponse
        {
            AttachmentId = existingAttachment.Id,
            ResourceId = existingAttachment.ResourceId,
            Name = existingAttachment.FileName,
            Checksum = existingAttachment.Checksum,
            Status = AttachmentStatus.Published,
            StatusText = "Duplicate",
            StatusChanged = existingAttachment.Created,
            DataLocationType = existingAttachment.DataLocationType,
            SendersReference = existingAttachment.SendersReference,
            FileName = existingAttachment.FileName,
            DisplayName = existingAttachment.DisplayName,
            Sender = existingAttachment.Sender,
        };
    }

    private abstract record MigrateAttachmentAttempt
    {
        public sealed record CreatedAttempt(MigrateAttachmentResponse Response) : MigrateAttachmentAttempt;
        public sealed record DuplicateAttempt : MigrateAttachmentAttempt;

        public static MigrateAttachmentAttempt Created(MigrateAttachmentResponse response) => new CreatedAttempt(response);
        public static MigrateAttachmentAttempt Duplicate() => new DuplicateAttempt();
    }
}
