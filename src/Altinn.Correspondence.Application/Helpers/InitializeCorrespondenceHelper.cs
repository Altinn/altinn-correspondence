using System;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class InitializeCorrespondenceHelper
    {
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
        private readonly IAttachmentStatusRepository _attachmentStatusRepository;
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IStorageRepository _storageRepository;
        private readonly IHostEnvironment _hostEnvironment;

        public InitializeCorrespondenceHelper(ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepositor, IAttachmentStatusRepository attachmentStatusRepository, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IHostEnvironment hostEnvironment)
        {
            _correspondenceRepository = correspondenceRepository;
            _correspondenceStatusRepository = correspondenceStatusRepositor;
            _attachmentStatusRepository = attachmentStatusRepository;
            _attachmentRepository = attachmentRepository;
            _hostEnvironment = hostEnvironment;
            _storageRepository = storageRepository;

        }
        public Error? ValidateCorrespondenceContent(CorrespondenceContentEntity content)
        {
            if (!TextValidation.ValidatePlainText(content?.MessageTitle))
            {
                return Errors.MessageTitleIsNotPlainText;
            }
            if (!TextValidation.ValidateMarkdown(content?.MessageBody))
            {
                return Errors.MessageBodyIsNotMarkdown;
            }
            if (!TextValidation.ValidateMarkdown(content?.MessageSummary))
            {
                return Errors.MessageSummaryIsNotMarkdown;
            }
            return null;
        }
        public Error? ValidateAttachmentFiles(List<IFormFile> files, List<CorrespondenceAttachmentEntity> attachments, bool isMultiUpload)
        {
            if (files.Count > 0 || isMultiUpload)
            {
                var maxUploadSize = long.Parse(int.MaxValue.ToString());
                foreach (var attachment in attachments)
                {
                    if (attachment.DataLocationUrl != null) continue;
                    if (files.Count == 0 && isMultiUpload) return Errors.MultipleCorrespondenceNoAttachments;
                    var file = files.FirstOrDefault(a => a.FileName == attachment.Name);
                    if (file == null) return Errors.UploadedFilesDoesNotMatchAttachments;
                    if (file?.Length > maxUploadSize || file?.Length == 0) return Errors.InvalidFileSize;
                }
            }
            return null;
        }

        public List<CorrespondenceNotificationEntity> ProcessNotifications(List<CorrespondenceNotificationEntity>? notifications, CancellationToken cancellationToken)
        {
            if (notifications == null) return new List<CorrespondenceNotificationEntity>();
            foreach (var notification in notifications)
            {
                notification.Statuses = new List<CorrespondenceNotificationStatusEntity>(){
                new CorrespondenceNotificationStatusEntity
                {
                     Status = "Initialized", //TODO create enums for notications?
                     StatusChanged = DateTimeOffset.UtcNow,
                     StatusText = "Initialized"
                }
            };
            }
            return notifications;
        }

        public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
        {
            var status = CorrespondenceStatus.Initialized;
            if (correspondence.Content != null && correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.Statuses.All(s => s.Status == AttachmentStatus.Published)))
            {
                if (_hostEnvironment.IsDevelopment() && correspondence.VisibleFrom < DateTime.UtcNow) status = CorrespondenceStatus.Published; // used to test on published correspondences in development
                else status = CorrespondenceStatus.ReadyForPublish;
            }
            return status;
        }

        public async Task<Error?> UploadAttachments(CorrespondenceEntity correspondence, List<IFormFile> attachments, CancellationToken cancellationToken)
        {
            UploadHelper uploadHelper = new UploadHelper(_correspondenceRepository, _correspondenceStatusRepository, _attachmentStatusRepository, _attachmentRepository, _storageRepository, _hostEnvironment);
            foreach (var file in attachments)
            {
                var attachment = correspondence.Content?.Attachments.FirstOrDefault(a => a.Name == file.FileName);
                if (attachment == null || attachment.Attachment == null)
                {
                    return Errors.UploadedFilesDoesNotMatchAttachments;
                }
                var uploadResponse = await uploadHelper.UploadAttachment(file.OpenReadStream(), attachment.AttachmentId, cancellationToken);
                var error = uploadResponse.Match(
                    _ => { return null; },
                    error => { return error; }
                );
                if (error != null) return error;
            }
            return null;
        }

        public async Task<AttachmentEntity> ProcessAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            AttachmentEntity? attachment = null;
            if (!String.IsNullOrEmpty(correspondenceAttachment.DataLocationUrl))
            {
                var existingAttachment = await _attachmentRepository.GetAttachmentByUrl(correspondenceAttachment.DataLocationUrl, cancellationToken);
                if (existingAttachment != null)
                {
                    attachment = existingAttachment;
                }
            }
            if (attachment == null)
            {
                var status = new List<AttachmentStatusEntity>(){
                    new AttachmentStatusEntity
                    {
                        Status = AttachmentStatus.Initialized,
                        StatusChanged = DateTimeOffset.UtcNow,
                        StatusText = AttachmentStatus.Initialized.ToString()
                    }
                };
                attachment = new AttachmentEntity
                {
                    ResourceId = correspondence.ResourceId,
                    FileName = correspondenceAttachment.Name,
                    Sender = correspondence.Sender,
                    SendersReference = correspondenceAttachment.SendersReference,
                    RestrictionName = correspondenceAttachment.RestrictionName,
                    ExpirationTime = correspondenceAttachment.ExpirationTime,
                    DataType = correspondenceAttachment.DataType,
                    DataLocationUrl = correspondenceAttachment.DataLocationUrl,
                    Statuses = status,
                    Created = DateTimeOffset.UtcNow
                };
            }
            return attachment;
        }
    }
}