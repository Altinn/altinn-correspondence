using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class MigrateUploadAttachmentHandler(
    IAltinnRegisterService altinnRegisterService,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    AttachmentHelper attachmentHelper,
    ILogger<MigrateUploadAttachmentHandler> logger) : IHandler<UploadAttachmentRequest, MigrateUploadAttachmentResponse>
{
    public async Task<OneOf<MigrateUploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var maxUploadSize = long.Parse(int.MaxValue.ToString());
        if (request.ContentLength > maxUploadSize || request.ContentLength == 0)
        {
            return AttachmentErrors.InvalidFileSize;
        }
        if (attachment.StatusHasBeen(AttachmentStatus.UploadProcessing))
        {
            return AttachmentErrors.FileAlreadyUploaded;
        }

        Guid senderPartyUuid;
        if(request.SenderPartyUuid.HasValue)
        {
            senderPartyUuid = request.SenderPartyUuid.Value;
        }
        else
        {
            var party = await altinnRegisterService.LookUpPartyById(attachment.Sender, cancellationToken);
            if (party?.PartyUuid is not Guid partyUuid)
            {
                return AuthorizationErrors.CouldNotFindPartyUuid;
            }
            
            senderPartyUuid = partyUuid;
        }

        return await TransactionWithRetriesPolicy.Execute<MigrateUploadAttachmentResponse>(async (cancellationToken) =>
        {
            var uploadResult = await attachmentHelper.UploadAttachment(request.UploadStream, request.AttachmentId, senderPartyUuid, cancellationToken);

            if (uploadResult.IsT1)
            {
                return AttachmentErrors.UploadFailed;
            }
            var savedAttachment = await attachmentRepository.GetAttachmentById(uploadResult.AsT0.AttachmentId, true, cancellationToken);
            if (savedAttachment == null)
            {
                return AttachmentErrors.UploadFailed;
            }

            await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity()
                {
                    Attachment = attachment,
                    AttachmentId = request.AttachmentId,
                    Status = AttachmentStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Published.ToString(),
                    PartyUuid = senderPartyUuid
                }, cancellationToken);

            var attachmentStatus = savedAttachment.GetLatestStatus();
            return new MigrateUploadAttachmentResponse
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
        }, logger, cancellationToken);
    }
}
