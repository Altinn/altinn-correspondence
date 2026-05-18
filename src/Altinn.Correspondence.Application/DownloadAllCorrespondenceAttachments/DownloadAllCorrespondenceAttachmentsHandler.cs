using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAllCorrespondenceAttachments;

public class DownloadAllCorrespondenceAttachmentsHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    AttachmentHelper attachmentHelper,
    ILogger<DownloadAllCorrespondenceAttachmentsHandler> logger) : IHandler<DownloadAllCorrespondenceAttachmentsRequest, DownloadAllCorrespondenceAttachmentsResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<DownloadAllCorrespondenceAttachmentsHandler> _logger = logger;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository = idempotencyKeyRepository;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;

    public async Task<OneOf<DownloadAllCorrespondenceAttachmentsResponse, Error>> Process(DownloadAllCorrespondenceAttachmentsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing download of all attachments for correspondence {CorrespondenceId}", request.CorrespondenceId);
        var operationTimestamp = DateTimeOffset.UtcNow;

        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            _logger.LogError("Correspondence with id {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var hasAccessToCorrespondence = await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken);
        if (!hasAccessToCorrespondence)
        {
            _logger.LogWarning("Access denied for correspondence {CorrespondenceId} - user does not have recipient access", request.CorrespondenceId);
            return AuthorizationErrors.NoAccessToResource;
        }

        var attachments = await _attachmentRepository.GetAttachmentsByCorrespondence(request.CorrespondenceId, cancellationToken);
        if (attachments is null || !attachments.Any())
        {
            _logger.LogError("No attachments found for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return AttachmentErrors.AttachmentNotFound;
        }

        var latestStatus = correspondence.GetHighestStatus();
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            _logger.LogWarning("Correspondence {CorrespondenceId} is not available for recipient - current status: {Status}", request.CorrespondenceId, latestStatus.Status);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        foreach (var attachment in attachments)
        {
            if (attachment.ResourceId != correspondence.ResourceId)
            {
                var hasAccess = await altinnAuthorizationService.CheckAttachmentAccessAsRecipient(user, correspondence, attachment, cancellationToken);
                if (!hasAccess)
                {
                    _logger.LogWarning("Access denied for attachment {AttachmentId} in correspondence {CorrespondenceId}", attachment.Id, request.CorrespondenceId);
                    return AuthorizationErrors.NoAccessToResource;
                }
            }
            var correspondenceAttachment = correspondence.Content?.Attachments?.FirstOrDefault(a => a.AttachmentId == attachment.Id);
            var cannotDownloadAttachmentError = attachmentHelper.ValidateDownloadCorrespondenceAttachment(attachment, correspondenceAttachment?.ExpirationTime);
            if (cannotDownloadAttachmentError is not null)
            {
                _logger.LogError("Attachment {AttachmentId} in correspondence {CorrespondenceId} cannot be downloaded due to its status", attachment.Id, request.CorrespondenceId);
                return cannotDownloadAttachmentError;
            }
        }

        var caller = user.GetCallerPartyUrn();
        var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            _logger.LogError("Could not find party UUID for caller {Caller}", caller.SanitizeForLogging());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }   

        const long maxAttachmentSizeForZip = 25_000_000; // 25 MB
        var totalSize = attachments.Sum(a => a.AttachmentSize);
        if (totalSize > maxAttachmentSizeForZip)
        {
            _logger.LogError("Total size of attachments for correspondence {CorrespondenceId} exceeds the maximum allowed for zip download: {TotalSize} bytes", request.CorrespondenceId, totalSize);
            return AttachmentErrors.TotalAttachmentSizeExceedsLimit;
        }
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in attachments)
            {
                var fileNameBytes = Encoding.UTF8.GetByteCount(attachment.FileName ?? string.Empty);
                if (fileNameBytes > 255)                {
                    _logger.LogInformation("Attachment {AttachmentId} in correspondence {CorrespondenceId} has a filename that exceeds the maximum length for zip entries. It will be truncated to fit within the limit.", attachment.Id, request.CorrespondenceId);
                    var ext = Path.GetExtension(attachment.FileName ?? string.Empty);
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(attachment.FileName ?? string.Empty);
                    var maxBaseBytes = 255 - Encoding.UTF8.GetByteCount(ext);
                    
                    var byteCount = 0;
                    var sb = new StringBuilder();
                    foreach (var c in nameWithoutExt)
                    {
                        var charBytes = Encoding.UTF8.GetByteCount(new[] { c });
                        if (byteCount + charBytes > maxBaseBytes)
                        {
                            break;
                        }
                        sb.Append(c);
                        byteCount += charBytes;
                    }
                    sb.Append(ext);
                    attachment.FileName = sb.ToString();
                }
                var baseName = attachment.FileName ?? attachment.Id.ToString();
                var uniqueName = baseName;
                var counter = 1;
                while (!usedEntryNames.Add(uniqueName))
                {
                    var ext = Path.GetExtension(baseName);
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
                    uniqueName = $"{nameWithoutExt}({counter}){ext}";
                    counter++;
                }
                var entry = archive.CreateEntry(uniqueName);
                using var entryStream = entry.Open();
                using var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
                await attachmentStream.CopyToAsync(entryStream, cancellationToken);
            }
        }
        zipStream.Position = 0;

        await TransactionWithRetriesPolicy.Execute<bool>(async (cancellationToken) =>
        {
            try
            {
                await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
                {
                    CorrespondenceId = request.CorrespondenceId,
                    Status = CorrespondenceStatus.AttachmentsDownloaded,
                    StatusText = "All attachments downloaded",
                    StatusChanged = DateTimeOffset.UtcNow,
                    PartyUuid = partyUuid
                }, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error when adding status to correspondence {CorrespondenceId}", request.CorrespondenceId);
            }
            return true;
        }, logger, cancellationToken);

        foreach (var attachment in attachments)
        {
            _backgroundJobClient.Enqueue<IDialogportenService>(s => s.CreateDownloadStartedActivity(
                request.CorrespondenceId,
                DialogportenActorType.Recipient,
                operationTimestamp,
                party.ExternalUrn ?? caller,
                attachment.DisplayName ?? attachment.FileName ?? string.Empty,
                attachment.Id.ToString()));
        }

        _logger.LogInformation("Successfully processed download of all attachments for correspondence {CorrespondenceId}", request.CorrespondenceId);
        return new DownloadAllCorrespondenceAttachmentsResponse { Stream = zipStream, zipFileName = attachmentHelper.GetZipFileNameForCorrespondence(correspondence) };
    }
}
