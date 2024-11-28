using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateUploadAttachment;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class MigrateUploadAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    IAttachmentRepository attachmentRepository,
    UploadHelper uploadHelper,
    ILogger<MigrateUploadAttachmentHandler> logger) : IHandler<UploadAttachmentRequest, MigrateUploadAttachmentResponse>
{
    public async Task<OneOf<MigrateUploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckMigrationAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
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
        var party = await altinnRegisterService.LookUpPartyById(attachment.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return Errors.CouldNotFindPartyUuid;
        }
        return await TransactionWithRetriesPolicy.Execute<MigrateUploadAttachmentResponse>(async (cancellationToken) =>
        {
            var uploadResult = await uploadHelper.UploadAttachment(request.UploadStream, request.AttachmentId, partyUuid, cancellationToken);

            if (uploadResult.IsT1)
            {
                return Errors.UploadFailed; // Why does this need to be commented out
            }
            var savedAttachment = await attachmentRepository.GetAttachmentById(uploadResult.AsT0.AttachmentId, true, cancellationToken);
            if (savedAttachment == null)
            {
                return Errors.UploadFailed; // Why does this need to be commented out
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
        }, logger, cancellationToken);
    }
}
