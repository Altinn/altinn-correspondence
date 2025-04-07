using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Hangfire;
using OneOf;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IAltinnRegisterService altinnRegisterService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<DownloadCorrespondenceAttachmentHandler> logger) : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{
    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken);
        if (correspondence is null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var attachment = await attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        var latestStatus = correspondence.GetHighestStatus();
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        
        return await TransactionWithRetriesPolicy.Execute<DownloadCorrespondenceAttachmentResponse>(async (cancellationToken) =>
        {
            try
            {
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = request.CorrespondenceId,
                    Status = CorrespondenceStatus.AttachmentsDownloaded,
                    StatusText = $"Attachment {attachment.Id} has been downloaded",
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when adding status to correspondence");
            }

            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(request.CorrespondenceId, DialogportenActorType.Recipient, DialogportenTextType.DownloadStarted, attachment.DisplayName ?? attachment.FileName));
            
            return new DownloadCorrespondenceAttachmentResponse()
            {
                FileName = attachment.FileName,
                Stream = attachmentStream
            };
        }, logger, cancellationToken);
    }
}
