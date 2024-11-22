using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class MigrateInitializeAttachmentHandler(
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    ILogger<MigrateInitializeAttachmentHandler> logger) : IHandler<InitializeAttachmentRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var hasAccess = await altinnAuthorizationService.CheckMigrationAccess(request.Attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var attachment = await attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
            await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                StatusChanged = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Initialized,
                StatusText = AttachmentStatus.Initialized.ToString()
            }, cancellationToken);
            return attachment.Id;
        }, logger, cancellationToken);
    }
}
