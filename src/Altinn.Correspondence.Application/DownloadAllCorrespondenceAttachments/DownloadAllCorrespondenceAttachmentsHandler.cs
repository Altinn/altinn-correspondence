using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
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
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    AttachmentHelper attachmentHelper,
    ILogger<DownloadAllCorrespondenceAttachmentsHandler> logger) : IHandler<DownloadAllCorrespondenceAttachmentsRequest, DownloadAllCorrespondenceAttachmentsResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<DownloadAllCorrespondenceAttachmentsHandler> _logger = logger;
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
        if (!latestStatus!.Status.IsAvailableForRecipient())
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

        var caller = user!.GetCallerPartyUrn()!;
        var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
        if (party?.Uuid is not Guid partyUuid)
        {
            _logger.LogError("Could not find party UUID for caller {Caller}", caller.SanitizeForLogging());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }   

        const long maxAttachmentSizeForZip = 2_000_000_000; // 2 GB
        var totalSize = attachments.Sum(a => a.AttachmentSize);
        if (totalSize > maxAttachmentSizeForZip)
        {
            _logger.LogError("Total size of attachments for correspondence {CorrespondenceId} exceeds the maximum allowed for zip download: {TotalSize} bytes", request.CorrespondenceId, totalSize);
            return AttachmentErrors.TotalAttachmentSizeExceedsLimit;
        }
        
        var entries = new List<ZipAttachmentEntry>(attachments.Count);
        var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attachment in attachments)
        {
            var originalFileName = attachment.FileName ?? attachment.Id.ToString();
            var zipEntryName = originalFileName;
            var fileNameBytes = Encoding.UTF8.GetByteCount(originalFileName);
            if (fileNameBytes > 255)
            {
                _logger.LogInformation("Attachment {AttachmentId} in correspondence {CorrespondenceId} has a filename that exceeds the maximum length for zip entries. It will be truncated to fit within the limit.", attachment.Id, request.CorrespondenceId);
                var ext = Path.GetExtension(originalFileName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                zipEntryName = TruncateToUtf8Bytes(nameWithoutExt, 255 - Encoding.UTF8.GetByteCount(ext)) + ext;
            }
            var baseName = zipEntryName;
            var uniqueName = baseName;
            var counter = 1;
            while (!usedEntryNames.Add(uniqueName))
            {
                var ext = Path.GetExtension(baseName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
                var suffix = $"({counter})";
                var truncatedName = TruncateToUtf8Bytes(nameWithoutExt, 255 - Encoding.UTF8.GetByteCount(ext) - Encoding.UTF8.GetByteCount(suffix));
                uniqueName = $"{truncatedName}{suffix}{ext}";
                counter++;
            }
            entries.Add(new ZipAttachmentEntry(attachment, uniqueName));
        }

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
                party.GetExternalUrn() ?? caller,
                attachment.DisplayName ?? attachment.FileName ?? string.Empty,
                attachment.Id.ToString()));
        }

        _logger.LogInformation("Successfully processed download of all attachments for correspondence {CorrespondenceId}", request.CorrespondenceId);
        return new DownloadAllCorrespondenceAttachmentsResponse
        {
            Entries = entries,
            ZipFileName = attachmentHelper.GetZipFileNameForCorrespondence(correspondence)
        };
    }

    /// <summary>
    /// Streams the attachments as a zip archive directly to <paramref name="output"/>, downloading each
    /// attachment from storage and copying it into the archive one at a time. Memory use stays small and
    /// constant regardless of total size.
    /// </summary>
    /// <remarks>
    /// Call only after <see cref="Process"/> has succeeded. Once writing begins the HTTP response is
    /// already committed, so a failure here (e.g. a storage error) cannot be turned into an error
    /// response — it surfaces as an aborted/truncated download.
    /// </remarks>
    public async Task WriteZip(IReadOnlyList<ZipAttachmentEntry> entries, Stream output, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (attachment, entryName) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
            await attachmentStream.CopyToAsync(entryStream, cancellationToken);
        }
    }

    private static string TruncateToUtf8Bytes(string text, int maxBytes)
    {
        var byteCount = 0;
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            var charBytes = Encoding.UTF8.GetByteCount(new[] { c });
            if (byteCount + charBytes > maxBytes)
                break;
            sb.Append(c);
            byteCount += charBytes;
        }
        return sb.ToString();
    }
}
