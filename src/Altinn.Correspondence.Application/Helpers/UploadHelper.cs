using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Azure;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UploadHelper
    {
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
        private readonly IAttachmentStatusRepository _attachmentStatusRepository;
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IStorageRepository _storageRepository;
        private readonly IHostEnvironment _hostEnvironment;

        public UploadHelper(ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepositor, IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment)
        {
            _correspondenceRepository = correspondenceRepository;
            _correspondenceStatusRepository = correspondenceStatusRepositor;
            _attachmentStatusRepository = attachmentStatusRepository;
            _attachmentRepository = attachmentRepository;
            _hostEnvironment = hostEnvironment;
            _storageRepository = storageRepository;

        }
        public async Task<OneOf<UploadAttachmentResponse, Error>> UploadAttachment(Stream file, Guid attachmentId, CancellationToken cancellationToken)
        {
            var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
            if (attachment == null)
            {
                return Errors.AttachmentNotFound;
            }

            var currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.UploadProcessing, cancellationToken);
            try
            {
                var (dataLocationUrl, checksum) = await _storageRepository.UploadAttachment(attachment, file, cancellationToken);

                var isValidUpdate = await _attachmentRepository.SetDataLocationUrl(attachment, AttachmentDataLocationType.AltinnCorrespondenceAttachment, dataLocationUrl, cancellationToken);

                if (string.IsNullOrWhiteSpace(attachment.Checksum))
                {
                    isValidUpdate |= await _attachmentRepository.SetChecksum(attachment, checksum, cancellationToken);
                }

                if (!isValidUpdate)
                {
                    await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.UploadFailed);
                    await _storageRepository.PurgeAttachment(attachment.Id, cancellationToken);
                    return Errors.UploadFailed;
                }
            }
            catch (DataLocationUrlException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.InvalidLocationUrl);
                return Errors.DataLocationNotFound;
            }
            catch (HashMismatchException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.ChecksumMismatch);
                return Errors.HashError;
            }
            catch (RequestFailedException)
            {
                await SetAttachmentStatus(attachmentId, AttachmentStatus.Failed, cancellationToken, AttachmentStatusText.UploadFailed);
                return Errors.UploadFailed;
            }

            if (_hostEnvironment.IsDevelopment()) // No malware scan when running locally
            {
                currentStatus = await SetAttachmentStatus(attachmentId, AttachmentStatus.Published, cancellationToken);
            }

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
    }
}