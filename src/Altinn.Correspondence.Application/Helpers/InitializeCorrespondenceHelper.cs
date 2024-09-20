using System;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class InitializeCorrespondenceHelper
    {
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly UploadHelper _uploadHelper;

        public InitializeCorrespondenceHelper(IAttachmentRepository attachmentRepository, IHostEnvironment hostEnvironment, UploadHelper uploadHelper)
        {
            _attachmentRepository = attachmentRepository;
            _hostEnvironment = hostEnvironment;
            _uploadHelper = uploadHelper;

        }
        public Error? ValidateDateConstraints(CorrespondenceEntity correspondence)
        {
            var visibleFrom = correspondence.VisibleFrom;
            if (correspondence.DueDateTime < DateTimeOffset.Now)
            {
                return Errors.DueDatePriorToday;
            }
            if (correspondence.DueDateTime < visibleFrom)
            {
                return Errors.DueDatePriorVisibleFrom;
            }
            if (correspondence.AllowSystemDeleteAfter < DateTimeOffset.Now)
            {
                return Errors.AllowSystemDeletePriorToday;
            }
            if (correspondence.AllowSystemDeleteAfter < visibleFrom)
            {
                return Errors.AllowSystemDeletePriorVisibleFrom;
            }
            if (correspondence.AllowSystemDeleteAfter < correspondence.DueDateTime)
            {
                return Errors.AllowSystemDeletePriorDueDate;
            }
            return null;
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
        public Error? ValidateAttachmentFiles(List<IFormFile> files, List<CorrespondenceAttachmentEntity> attachments, bool isUpload)
        {
            if (files.Count > 0 || isUpload)
            {
                var maxUploadSize = long.Parse(int.MaxValue.ToString());
                if (isUpload && attachments.Count == 0) return Errors.UploadCorrespondenceNoAttachments;
                foreach (var attachment in attachments)
                {
                    if (attachment.Attachment?.DataLocationUrl != null) continue;
                    if (files.Count == 0 && isUpload) return Errors.UploadCorrespondenceNoAttachments;
                    var file = files.FirstOrDefault(a => a.FileName == attachment.Attachment?.FileName);
                    if (file == null) return Errors.UploadedFilesDoesNotMatchAttachments;
                    if (file?.Length > maxUploadSize || file?.Length == 0) return Errors.InvalidFileSize;
                }
            }
            return null;
        }

        public List<CorrespondenceNotificationEntity> ProcessNotifications(List<CorrespondenceNotificationEntity>? notifications, CancellationToken cancellationToken)
        {
            if (notifications == null) return new List<CorrespondenceNotificationEntity>();
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

        public async Task<Error?> UploadAttachments(List<AttachmentEntity> correspondenceAttachments, List<IFormFile> files, CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                var attachment = correspondenceAttachments.FirstOrDefault(a => a.FileName.ToLower() == file.FileName.ToLower());

                if (attachment == null)
                {
                    return Errors.UploadedFilesDoesNotMatchAttachments;
                }
                OneOf<UploadAttachmentResponse, Error> uploadResponse;
                await using (var f = file.OpenReadStream())
                {
                    uploadResponse = await _uploadHelper.UploadAttachment(f, attachment.Id, cancellationToken);
                }
                var error = uploadResponse.Match(
                    _ => { return null; },
                    error => { return error; }
                );
                if (error != null) return error;
            }
            return null;
        }

        public async Task<List<AttachmentEntity>?> GetExistingAttachments(List<Guid> attachmentIds, CancellationToken cancellationToken)
        {
            var attachments = new List<AttachmentEntity>();
            foreach (var attachmentId in attachmentIds)
            {
                var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, false, cancellationToken);
                if (attachment == null) return null;
                attachments.Add(attachment);
            }
            return attachments;
        }

        public async Task<AttachmentEntity> ProcessNewAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, CancellationToken cancellationToken)
        {
            var status = new List<AttachmentStatusEntity>(){
                new AttachmentStatusEntity
                {
                    Status = AttachmentStatus.Initialized,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Initialized.ToString()
                }
            };
            var attachment = correspondenceAttachment.Attachment!;
            attachment.Statuses = status;
            return await _attachmentRepository.InitializeAttachment(attachment, cancellationToken);
        }
    }
}