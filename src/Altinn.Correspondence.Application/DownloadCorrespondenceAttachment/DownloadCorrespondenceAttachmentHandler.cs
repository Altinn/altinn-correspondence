using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    ILogger<DownloadCorrespondenceAttachmentHandler> logger) : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<DownloadCorrespondenceAttachmentHandler> _logger = logger;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository = idempotencyKeyRepository;

    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing download for correspondence {correspondenceId} and attachment {attachmentId}", request.CorrespondenceId, request.AttachmentId);

        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            _logger.LogError("Correspondence with id {correspondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var attachment = await attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            _logger.LogError("Attachment with id {attachmentId} not found in correspondence {correspondenceId}", request.AttachmentId, request.CorrespondenceId);
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

        // Check for existing idempotency key
        var existingKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAsync(
            request.CorrespondenceId, 
            request.AttachmentId, 
            StatusAction.AttachmentDownloaded, 
            cancellationToken);

        string activityId;
        if (existingKey != null)
        {
            // Use existing activity ID
            activityId = existingKey.Id.ToString();
        }
        else
        {
            // Create new idempotency key
            activityId = Guid.NewGuid().ToString();
            var idempotencyKey = new IdempotencyKeyEntity
            {
                Id = Guid.Parse(activityId),
                CorrespondenceId = request.CorrespondenceId,
                AttachmentId = request.AttachmentId,
                StatusAction = StatusAction.AttachmentDownloaded
            };
            await _idempotencyKeyRepository.CreateAsync(idempotencyKey, cancellationToken);
        }


        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }

        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
        
        
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

            _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => 
            dialogportenService.CreateInformationActivity(
                request.CorrespondenceId, 
                DialogportenActorType.ServiceOwner, 
                DialogportenTextType.DownloadStarted,  
                attachment.DisplayName ?? attachment.FileName,
                request.AttachmentId.ToString()));
            return new DownloadCorrespondenceAttachmentResponse()
            {
                FileName = attachment.FileName,
                Stream = attachmentStream
            };
        }, logger, cancellationToken);
    }
}
