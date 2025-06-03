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
        _logger.LogInformation("Processing download for correspondence {CorrespondenceId} and attachment {AttachmentId}", request.CorrespondenceId, request.AttachmentId);
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            _logger.LogError("Correspondence with id {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        _logger.LogDebug("Retrieved correspondence {CorrespondenceId} with status {Status}", request.CorrespondenceId, correspondence.GetHighestStatus()?.Status);
        var attachment = await attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            _logger.LogError("Attachment with id {AttachmentId} not found in correspondence {CorrespondenceId}", request.AttachmentId, request.CorrespondenceId);
            return AttachmentErrors.AttachmentNotFound;
        }
        _logger.LogDebug("Retrieved attachment {AttachmentId} with filename {FileName}", request.AttachmentId, attachment.FileName);
        var hasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken);
        if (!hasAccess)
        {
            _logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }
        _logger.LogDebug("User has recipient access to correspondence {CorrespondenceId}", request.CorrespondenceId);
        var latestStatus = correspondence.GetHighestStatus();
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            _logger.LogWarning("Correspondence {CorrespondenceId} is not available for recipient - current status: {Status}", request.CorrespondenceId, latestStatus.Status);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        _logger.LogDebug("Correspondence {CorrespondenceId} is available for recipient with status {Status}", request.CorrespondenceId, latestStatus.Status);
        // Check for existing idempotency key
        var existingKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
            request.CorrespondenceId, 
            request.AttachmentId, 
            StatusAction.AttachmentDownloaded,
            IdempotencyType.DialogportenActivity,
            cancellationToken);

        string activityId;
        if (existingKey != null)
        {
            _logger.LogDebug("Found existing idempotency key {KeyId} for correspondence {CorrespondenceId} and attachment {AttachmentId}", 
                existingKey.Id, request.CorrespondenceId, request.AttachmentId);
            activityId = existingKey.Id.ToString();
        }
        else
        {
            activityId = Guid.NewGuid().ToString();
            _logger.LogDebug("Creating new idempotency key {KeyId} for correspondence {CorrespondenceId} and attachment {AttachmentId}", 
                activityId, request.CorrespondenceId, request.AttachmentId);
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
            _logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        _logger.LogDebug("Retrieved party UUID {PartyUuid} for organization {OrganizationId}", partyUuid, user.GetCallerOrganizationId());
        _logger.LogDebug("Downloading attachment {AttachmentId} from storage", request.AttachmentId);
        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
        
        return await TransactionWithRetriesPolicy.Execute<DownloadCorrespondenceAttachmentResponse>(async (cancellationToken) =>
        {
            try
            {
                _logger.LogDebug("Adding AttachmentsDownloaded status for correspondence {CorrespondenceId}", request.CorrespondenceId);
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
                _logger.LogError(e, "Error when adding status to correspondence {CorrespondenceId}", request.CorrespondenceId);
            }

            _logger.LogDebug("Enqueueing Dialogporten activity for correspondence {CorrespondenceId}", request.CorrespondenceId);
            _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => 
            dialogportenService.CreateInformationActivity(
                request.CorrespondenceId, 
                DialogportenActorType.ServiceOwner, 
                DialogportenTextType.DownloadStarted,  
                attachment.DisplayName ?? attachment.FileName,
                request.AttachmentId.ToString(),
                DateTime.Now.ToString()));
            _logger.LogInformation("Successfully processed download request for correspondence {CorrespondenceId} and attachment {AttachmentId}", 
                request.CorrespondenceId, request.AttachmentId);
            return new DownloadCorrespondenceAttachmentResponse()
            {
                FileName = attachment.FileName,
                Stream = attachmentStream
            };
        }, logger, cancellationToken);
    }
}
