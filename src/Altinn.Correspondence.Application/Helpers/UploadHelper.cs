using System;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UploadHelper
    {
        private IAttachmentStatusRepository _attachmentStatusRepository;
        private IAttachmentRepository _attachmentRepository;
        private IStorageRepository _storageRepository;
        private IHostEnvironment _hostEnvironment;

        public UploadHelper(IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment)
        {
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
            var currentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachmentId,
                Status = AttachmentStatus.UploadProcessing,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.UploadProcessing.ToString()
            };
            await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken); // TODO, with malware scan this should be set after upload
            var dataLocationUrl = await _storageRepository.UploadAttachment(attachmentId, file, cancellationToken);
            if (dataLocationUrl is null)
            {
                currentStatus = new AttachmentStatusEntity
                {
                    AttachmentId = attachmentId,
                    Status = AttachmentStatus.Failed,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Failed.ToString()
                };
                await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
                return Errors.UploadFailed;
            }
            await _attachmentRepository.SetDataLocationUrl(attachment, AttachmentDataLocationType.AltinnCorrespondenceAttachment, dataLocationUrl, cancellationToken);
            if (_hostEnvironment.IsDevelopment()) // No malware scan when running locally
            {
                currentStatus = new AttachmentStatusEntity
                {
                    AttachmentId = attachment.Id,
                    Status = AttachmentStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Published.ToString()
                };
                await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
            }
            return new UploadAttachmentResponse()
            {
                AttachmentId = attachment.Id,
                Status = currentStatus.Status,
                StatusChanged = currentStatus.StatusChanged,
                StatusText = currentStatus.StatusText
            };
        }
    }
}