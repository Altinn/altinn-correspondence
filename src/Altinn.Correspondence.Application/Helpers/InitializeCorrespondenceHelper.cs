using Altinn.Correspondence.Application.InitializeCorrespondences;
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
            var RequestedPublishTime = correspondence.RequestedPublishTime;
            if (correspondence.DueDateTime < DateTimeOffset.UtcNow)
            {
                return Errors.DueDatePriorToday;
            }
            if (correspondence.DueDateTime < RequestedPublishTime)
            {
                return Errors.DueDatePriorRequestedPublishTime;
            }
            if (correspondence.AllowSystemDeleteAfter < DateTimeOffset.UtcNow)
            {
                return Errors.AllowSystemDeletePriorToday;
            }
            if (correspondence.AllowSystemDeleteAfter < RequestedPublishTime)
            {
                return Errors.AllowSystemDeletePriorRequestedPublishTime;
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
        public Error? ValidateNotification(NotificationRequest notification)
        {
            if (notification.NotificationTemplate == NotificationTemplate.GenericAltinnMessage || notification.NotificationTemplate == NotificationTemplate.Altinn2Message) return null;

            var reminderNotificationChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel;
            if (notification.NotificationChannel == NotificationChannel.Email && (string.IsNullOrEmpty(notification.EmailBody) || string.IsNullOrEmpty(notification.EmailSubject)))
            {
                return Errors.MissingEmailContent;
            }
            if (reminderNotificationChannel == NotificationChannel.Email && notification.SendReminder && (string.IsNullOrEmpty(notification.ReminderEmailBody) || string.IsNullOrEmpty(notification.ReminderEmailSubject)))
            {
                return Errors.MissingEmailReminderNotificationContent;
            }
            if (notification.NotificationChannel == NotificationChannel.Sms && string.IsNullOrEmpty(notification.SmsBody))
            {
                return Errors.MissingSmsContent;
            }
            if (reminderNotificationChannel == NotificationChannel.Sms && notification.SendReminder && string.IsNullOrEmpty(notification.ReminderSmsBody))
            {
                return Errors.MissingSmsReminderNotificationContent;
            }
            if ((notification.NotificationChannel == NotificationChannel.EmailPreferred || notification.NotificationChannel == NotificationChannel.SmsPreferred) &&
                (string.IsNullOrEmpty(notification.EmailBody) || string.IsNullOrEmpty(notification.EmailSubject) || string.IsNullOrEmpty(notification.SmsBody)))
            {
                return Errors.MissingPrefferedNotificationContent;
            }
            if ((reminderNotificationChannel == NotificationChannel.EmailPreferred || reminderNotificationChannel == NotificationChannel.SmsPreferred) &&
                notification.SendReminder && (string.IsNullOrEmpty(notification.ReminderEmailBody) || string.IsNullOrEmpty(notification.ReminderEmailSubject) || string.IsNullOrEmpty(notification.ReminderSmsBody)))
            {
                return Errors.MissingPrefferedReminderNotificationContent;
            }
            return null;
        }

        /// <summary>
        /// Validates that the uploaded files match the attachments in the correspondence
        /// </summary>
        public static Error? ValidateAttachmentFiles(List<IFormFile> files, List<CorrespondenceAttachmentEntity> attachments)
        {
            var maxUploadSize = long.Parse(int.MaxValue.ToString());
            foreach (var attachment in attachments)
            {
                if (attachment.Attachment?.DataLocationUrl != null) continue;
                var file = files.FirstOrDefault(a => a.FileName == attachment.Attachment?.FileName);
                if (file == null) return Errors.UploadedFilesDoesNotMatchAttachments;
                if (file?.Length > maxUploadSize || file?.Length == 0) return Errors.InvalidFileSize;
            }
            return null;
        }

        /// <summary>
        /// Get existing attachments from the database
        /// </summary>
        /// <remarks>
        /// If the attachment is not found in the database, it is not included in the returned list
        /// </remarks>
        /// <returns>A list of the attachments found</returns>
        public async Task<List<AttachmentEntity>> GetExistingAttachments(List<Guid> attachmentIds)
        {
            var attachments = new List<AttachmentEntity>();
            foreach (var attachmentId in attachmentIds)
            {
                var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true);
                if (attachment is not null)
                {
                    attachments.Add(attachment);
                }
            }
            return attachments;
        }

        public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
        {
            var status = CorrespondenceStatus.Initialized;
            if (correspondence.Content != null && correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.Statuses.Any(s => s.Status == AttachmentStatus.Published)))
            {
                if (_hostEnvironment.IsDevelopment() && correspondence.RequestedPublishTime < DateTimeOffset.UtcNow) status = CorrespondenceStatus.Published; // used to test on published correspondences in development
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