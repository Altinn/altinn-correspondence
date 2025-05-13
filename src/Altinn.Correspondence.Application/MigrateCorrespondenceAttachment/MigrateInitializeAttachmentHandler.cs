using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class MigrateInitializeAttachmentHandler(
    IAttachmentRepository attachmentRepository,
    IAltinnRegisterService altinnRegisterService,
    IAttachmentStatusRepository attachmentStatusRepository,
    ILogger<MigrateInitializeAttachmentHandler> logger) : IHandler<MigrateInitializeAttachmentRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(MigrateInitializeAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var party = await altinnRegisterService.LookUpPartyById(request.Attachment.Sender, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        var attachmentId = await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var attachment = await attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
            await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                StatusChanged = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Initialized,
                StatusText = AttachmentStatus.Initialized.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);
            return attachment.Id;
        }, logger, cancellationToken);
        return attachmentId;
    }
}
