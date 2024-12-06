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
    public class InitializeCorrespondenceHelper(IAttachmentRepository attachmentRepository, IHostEnvironment hostEnvironment, UploadHelper uploadHelper)
    {
        private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
        private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
        private readonly UploadHelper _uploadHelper = uploadHelper;

        public Error? ValidateDateConstraints(CorrespondenceEntity correspondence)
        {
            var RequestedPublishTime = correspondence.RequestedPublishTime;
            if (correspondence.DueDateTime is not null)
            {
                if (correspondence.DueDateTime < DateTimeOffset.UtcNow)
                {
                    return Errors.DueDatePriorToday;
                }
                if (correspondence.DueDateTime < RequestedPublishTime)
                {
                    return Errors.DueDatePriorRequestedPublishTime;
                }
            }
            if (correspondence.AllowSystemDeleteAfter is not null)
            {
                if (correspondence.AllowSystemDeleteAfter < DateTimeOffset.UtcNow)
                {
                    return Errors.AllowSystemDeletePriorToday;
                }
                if (correspondence.AllowSystemDeleteAfter < RequestedPublishTime)
                {
                    return Errors.AllowSystemDeletePriorRequestedPublishTime;
                }
                if (correspondence.DueDateTime is not null && correspondence.AllowSystemDeleteAfter < correspondence.DueDateTime)
                {
                    return Errors.AllowSystemDeletePriorDueDate;
                }
            }
            return null;
        }
        public Error? ValidateCorrespondenceContent(CorrespondenceContentEntity? content)
        {
            if (content == null)
            {
                return Errors.MissingContent;
            }
            if (string.IsNullOrWhiteSpace(content.MessageTitle))
            {
                return Errors.MessageTitleEmpty;
            }
            if (!TextValidation.ValidatePlainText(content.MessageTitle))
            {
                return Errors.MessageTitleIsNotPlainText;
            }
            if (string.IsNullOrWhiteSpace(content.MessageBody))
            {
                return Errors.MessageBodyEmpty;
            }
            if (!TextValidation.ValidateMarkdown(content.MessageBody))
            {
                return Errors.MessageBodyIsNotMarkdown;
            }
            if (string.IsNullOrWhiteSpace(content.MessageSummary))
            {
                return Errors.MessageSummaryEmpty;
            }
            if (!TextValidation.ValidateMarkdown(content.MessageSummary))
            {
                return Errors.MessageSummaryIsNotMarkdown;
            }
            if (!IsLanguageValid(content.Language))
            {
                return Errors.InvalidLanguage;
            }

            return null;
        }
        private static bool IsLanguageValid(string language)
        {
            List<string> supportedLanguages = ["nb", "nn", "en"];
            return supportedLanguages.Contains(language.ToLower());
        }
        public Error? ValidateNotification(NotificationRequest notification)
        {
            var skipContentCheck = notification.NotificationTemplate == NotificationTemplate.GenericAltinnMessage || notification.NotificationTemplate == NotificationTemplate.Altinn2Message;
            var reminderNotificationChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel;
            var recipientsHasEmail = notification.RecipientOverrides == null || notification.RecipientOverrides.Count == 0 || !notification.RecipientOverrides.Any(r => r.recipients.Any(r2 => string.IsNullOrEmpty(r2.EmailAddress) && string.IsNullOrEmpty(r2.OrganizationNumber) && string.IsNullOrEmpty(r2.NationalIdentityNumber)));
            var recipientsHasSms = notification.RecipientOverrides == null || notification.RecipientOverrides.Count == 0 || !notification.RecipientOverrides.Any(r => r.recipients.Any(r2 => string.IsNullOrEmpty(r2.MobileNumber) && string.IsNullOrEmpty(r2.OrganizationNumber) && string.IsNullOrEmpty(r2.NationalIdentityNumber)));
            var recipientsHasEmailAndSms = notification.RecipientOverrides == null || notification.RecipientOverrides.Count == 0 || !notification.RecipientOverrides.Any(r => r.recipients.Any(r2 => string.IsNullOrEmpty(r2.EmailAddress) && string.IsNullOrEmpty(r2.MobileNumber) && string.IsNullOrEmpty(r2.OrganizationNumber) && string.IsNullOrEmpty(r2.NationalIdentityNumber)));

            if (notification.NotificationChannel == NotificationChannel.Email)
            {
                if (!recipientsHasEmail) return Errors.MissingEmailRecipient;
                if (notification.SendReminder && notification.ReminderNotificationChannel != NotificationChannel.Email && !recipientsHasEmailAndSms)
                {
                    return Errors.MissingEmailAndSmsRecipient;
                }
                if (!skipContentCheck && (string.IsNullOrEmpty(notification.EmailBody) || string.IsNullOrEmpty(notification.EmailSubject)))
                {
                    return Errors.MissingEmailContent;
                }
            }
            if (notification.NotificationChannel == NotificationChannel.Sms)
            {
                if (!recipientsHasSms) return Errors.MissingSmsRecipient;
                if (notification.SendReminder && notification.ReminderNotificationChannel != NotificationChannel.Sms && !recipientsHasEmailAndSms)
                {
                    return Errors.MissingEmailAndSmsRecipient;
                }
                if (!skipContentCheck)
                {
                    if (string.IsNullOrEmpty(notification.SmsBody))
                    {
                        return Errors.MissingSmsContent;
                    }
                }
            }
            if (notification.NotificationChannel == NotificationChannel.EmailPreferred || notification.NotificationChannel == NotificationChannel.SmsPreferred)
            {
                if (!recipientsHasEmailAndSms) return Errors.MissingEmailAndSmsRecipient;
                if (!skipContentCheck)
                {
                    if (string.IsNullOrEmpty(notification.EmailBody) || string.IsNullOrEmpty(notification.EmailSubject) || string.IsNullOrEmpty(notification.SmsBody))
                    {
                        return Errors.MissingPreferredNotificationContent;
                    }
                }
            }
            if (notification.SendReminder && !skipContentCheck)
            {
                if (notification.ReminderNotificationChannel == null) return Errors.MissingReminderNotificationChannel;
                if (notification.ReminderNotificationChannel == NotificationChannel.Email && (string.IsNullOrEmpty(notification.ReminderEmailBody) || string.IsNullOrEmpty(notification.ReminderEmailSubject)))
                {
                    return Errors.MissingEmailReminderNotificationContent;
                }
                if (notification.ReminderNotificationChannel == NotificationChannel.Sms && string.IsNullOrEmpty(notification.ReminderSmsBody))
                {
                    return Errors.MissingSmsReminderNotificationContent;
                }
                if (notification.ReminderNotificationChannel == NotificationChannel.EmailPreferred || notification.ReminderNotificationChannel == NotificationChannel.SmsPreferred)
                {
                    if (string.IsNullOrEmpty(notification.ReminderEmailBody) || string.IsNullOrEmpty(notification.ReminderEmailSubject) || string.IsNullOrEmpty(notification.ReminderSmsBody))
                    {
                        return Errors.MissingPreferredReminderNotificationContent;
                    }
                }
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
        public async Task<OneOf<List<AttachmentEntity>, Error>> GetExistingAttachments(List<Guid> attachmentIds, string sender)
        {
            var attachments = new List<AttachmentEntity>();
            foreach (var attachmentId in attachmentIds)
            {
                var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true);
                if (attachment is not null)
                {
                    if (attachment.Sender != sender) return Errors.InvalidSenderForAttachment;
                    attachments.Add(attachment);
                }
            }
            return attachments;
        }

        public CorrespondenceStatus GetInitializeCorrespondenceStatus(CorrespondenceEntity correspondence)
        {
            var status = CorrespondenceStatus.Initialized;
            if (correspondence.Content != null && correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.StatusHasBeen(AttachmentStatus.Published)))
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