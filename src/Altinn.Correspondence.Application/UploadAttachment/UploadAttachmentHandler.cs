using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class UploadAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IHostEnvironment hostEnvironment, IEventBus eventBus) : IHandler<UploadAttachmentRequest, UploadAttachmentResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IEventBus _eventBus = eventBus;

    public async Task<OneOf<UploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var maxUploadSize = long.Parse(int.MaxValue.ToString());
        if (request.ContentLength > maxUploadSize || request.ContentLength == 0)
        {
            return Errors.InvalidFileSize;
        }
        if (attachment.Statuses.Any(status => status.Status == AttachmentStatus.UploadProcessing))
        {
            return Errors.InvalidUploadAttachmentStatus;
        }

        var currentStatus = await SetAttachmentStatus(request.AttachmentId, AttachmentStatus.UploadProcessing, cancellationToken);
        try
        {
            await UploadAttachmentAndSetMetadata(attachment, request.UploadStream, cancellationToken);
        }
        catch (DataLocationUrlException)
        {
            await SetAttachmentStatus(request.AttachmentId, AttachmentStatus.Failed, cancellationToken, "Could not get data location url");
            return Errors.UploadFailed;
        }
        catch (HashMismatchException)
        {
            await SetAttachmentStatus(request.AttachmentId, AttachmentStatus.Failed, cancellationToken, "Checksum mismatch");
            return Errors.UploadFailed;
        }
        catch (Exception)
        {
            await SetAttachmentStatus(request.AttachmentId, AttachmentStatus.Failed, cancellationToken, "Upload failed");
            return Errors.UploadFailed;
        }

        if (_hostEnvironment.IsDevelopment()) // No malware scan when running locally
        {
            currentStatus = await SetAttachmentStatus(request.AttachmentId, AttachmentStatus.Published, cancellationToken);
        }
        await CheckCorrespondenceStatusesAfterUploadAndPublish(attachment.Id, cancellationToken);

        return new UploadAttachmentResponse()
        {
            AttachmentId = attachment.Id,
            Status = currentStatus.Status,
            StatusChanged = currentStatus.StatusChanged,
            StatusText = currentStatus.StatusText
        };
    }

    private async Task<AttachmentStatusEntity> SetAttachmentStatus(Guid attachmentId, AttachmentStatus status, CancellationToken cancellationToken, string statusText = null)
    {
        var currentStatus = new AttachmentStatusEntity
        {
            AttachmentId = attachmentId,
            Status = status,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = statusText ?? status.ToString()
        };
        await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
        return currentStatus;
    }
    private async Task UploadAttachmentAndSetMetadata(AttachmentEntity attachment, Stream uploadStream, CancellationToken cancellationToken)
{
    var (dataLocationUrl, checksum) = await _storageRepository.UploadAttachment(attachment, uploadStream, cancellationToken);

    await _attachmentRepository.SetDataLocationUrl(attachment, AttachmentDataLocationType.AltinnCorrespondenceAttachment, dataLocationUrl, cancellationToken);

    if (string.IsNullOrWhiteSpace(attachment.Checksum))
    {
        await _attachmentRepository.SetChecksum(attachment, checksum, cancellationToken);
    }
}

    public async Task CheckCorrespondenceStatusesAfterUploadAndPublish(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return;
        }

        var correspondences = await _correspondenceRepository.GetNonPublishedCorrespondencesByAttachmentId(attachment.Id, cancellationToken);
        if (correspondences.Count == 0)
        {
            return;
        }

        var list = new List<CorrespondenceStatusEntity>();
        foreach (var correspondenceId in correspondences)
        {
            list.Add(
                new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.ReadyForPublish,
                    StatusChanged = DateTime.UtcNow,
                    StatusText = CorrespondenceStatus.ReadyForPublish.ToString()
                }
            );
        }
        await _correspondenceStatusRepository.AddCorrespondenceStatuses(list, cancellationToken);
        return;
    }
}
