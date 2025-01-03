using System.Text.RegularExpressions;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Notifications.Core.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.Helpers
{
    public class InitializeCorrespondenceHelper(
        IAttachmentRepository attachmentRepository,
        IHostEnvironment hostEnvironment,
        AttachmentHelper attachmentHelper,
        MobileNumberHelper mobileNumberHelper,
        ILogger<InitializeCorrespondenceHelper> logger)
    {
        private static readonly Regex emailRegex = new Regex(@"((""[^\\""]+"")|(([a-zA-Z0-9!#$%&'*+\-=?\^_`{|}~])+(\.([a-zA-Z0-9!#$%&'*+\-=?\^_`{|}~])+)*))@((((([a-zA-Z0-9æøåÆØÅ]([a-zA-Z0-9\-æøåÆØÅ]{0,61})[a-zA-Z0-9æøåÆØÅ]\.)|[a-zA-Z0-9æøåÆØÅ]\.){1,9})([a-zA-Z]{2,14}))|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))");

        public Error? ValidateDateConstraints(CorrespondenceEntity correspondence)
        {
            var RequestedPublishTime = correspondence.RequestedPublishTime;
            if (correspondence.DueDateTime is not null)
            {
                if (correspondence.DueDateTime < DateTimeOffset.UtcNow)
                {
                    return CorrespondenceErrors.DueDatePriorToday;
                }
                if (correspondence.DueDateTime < RequestedPublishTime)
                {
                    return CorrespondenceErrors.DueDatePriorRequestedPublishTime;
                }
            }
            if (correspondence.AllowSystemDeleteAfter is not null)
            {
                if (correspondence.AllowSystemDeleteAfter < DateTimeOffset.UtcNow)
                {
                    return CorrespondenceErrors.AllowSystemDeletePriorToday;
                }
                if (correspondence.AllowSystemDeleteAfter < RequestedPublishTime)
                {
                    return CorrespondenceErrors.AllowSystemDeletePriorRequestedPublishTime;
                }
                if (correspondence.DueDateTime is not null && correspondence.AllowSystemDeleteAfter < correspondence.DueDateTime)
                {
                    return CorrespondenceErrors.AllowSystemDeletePriorDueDate;
                }
            }
            return null;
        }
        public Error? ValidateCorrespondenceContent(CorrespondenceContentEntity? content)
        {
            if (content == null)
            {
                return CorrespondenceErrors.MissingContent;
            }
            if (string.IsNullOrWhiteSpace(content.MessageTitle))
            {
                return CorrespondenceErrors.MessageTitleEmpty;
            }
            if (!TextValidation.ValidatePlainText(content.MessageTitle))
            {
                return CorrespondenceErrors.MessageTitleIsNotPlainText;
            }
            if (string.IsNullOrWhiteSpace(content.MessageBody))
            {
                return CorrespondenceErrors.MessageBodyEmpty;
            }
            if (!TextValidation.ValidateMarkdown(content.MessageBody))
            {
                return CorrespondenceErrors.MessageBodyIsNotMarkdown;
            }
            if (string.IsNullOrWhiteSpace(content.MessageSummary))
            {
                return CorrespondenceErrors.MessageSummaryEmpty;
            }
            if (!TextValidation.ValidateMarkdown(content.MessageSummary))
            {
                return CorrespondenceErrors.MessageSummaryIsNotMarkdown;
            }
            if (!IsLanguageValid(content.Language))
            {
                return CorrespondenceErrors.InvalidLanguage;
            }

            return null;
        }
        private static bool IsLanguageValid(string language)
        {
            List<string> supportedLanguages = ["nb", "nn", "en"];
            return supportedLanguages.Contains(language.ToLower());
        }
        public Error? ValidateNotification(NotificationRequest notification, List<string> recipients)
        {
            var customRecipientError = ValidateRecipientOverrides(notification, recipients);
            if (customRecipientError != null)
            {
                return customRecipientError;
            }

            var contentError = ValidateNotificationContent(notification);
            if (contentError != null)
            {
                return contentError;
            }
            return null;
        }

        /// <summary>
        /// Validate the content of the notification.
        /// </summary>
        private Error? ValidateNotificationContent(NotificationRequest notification)
        {
            if (notification.NotificationTemplate == NotificationTemplate.GenericAltinnMessage || notification.NotificationTemplate == NotificationTemplate.Altinn2Message) return null;

            var reminderNotificationChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel;
            if (notification.NotificationChannel == NotificationChannel.Email && (string.IsNullOrEmpty(notification.EmailBody) || string.IsNullOrEmpty(notification.EmailSubject)))
            {
                return NotificationErrors.MissingEmailContent;
            }
            if (reminderNotificationChannel == NotificationChannel.Email && notification.SendReminder && (string.IsNullOrEmpty(notification.ReminderEmailBody) || string.IsNullOrEmpty(notification.ReminderEmailSubject)))
            {
                return NotificationErrors.MissingEmailReminderContent;
            }
            if (notification.NotificationChannel == NotificationChannel.Sms && string.IsNullOrEmpty(notification.SmsBody))
            {
                return NotificationErrors.MissingSmsContent;
            }
            if (reminderNotificationChannel == NotificationChannel.Sms && notification.SendReminder && string.IsNullOrEmpty(notification.ReminderSmsBody))
            {
                return NotificationErrors.MissingSmsReminderContent;
            }
            if ((notification.NotificationChannel == NotificationChannel.EmailPreferred || notification.NotificationChannel == NotificationChannel.SmsPreferred) &&
                (string.IsNullOrEmpty(notification.EmailBody) || string.IsNullOrEmpty(notification.EmailSubject) || string.IsNullOrEmpty(notification.SmsBody)))
            {
                return NotificationErrors.MissingPreferredChannel;
            }
            if ((reminderNotificationChannel == NotificationChannel.EmailPreferred || reminderNotificationChannel == NotificationChannel.SmsPreferred) &&
                notification.SendReminder && (string.IsNullOrEmpty(notification.ReminderEmailBody) || string.IsNullOrEmpty(notification.ReminderEmailSubject) || string.IsNullOrEmpty(notification.ReminderSmsBody)))
            {
                return NotificationErrors.MissingPreferredReminderChannel;
            }
            return null;
        }

        /// <summary>
        /// Validate that the recipient overrides for a notification.
        /// </summary>
        public Error? ValidateRecipientOverrides(NotificationRequest notification, List<string> recipients)
        {
            var customRecipients = notification.CustomNotificationRecipients ?? [];
            if (customRecipients.Count == 0)
            {
                return null;
            }

            foreach (var recipientToOverride in customRecipients)
            {
                if (!recipients.Any(recipient => recipient.WithoutPrefix() == recipientToOverride.RecipientToOverride.WithoutPrefix()))
                {
                    return NotificationErrors.CouldNotFindRecipientToOverride(recipientToOverride.RecipientToOverride);
                }
            }

            foreach (var recipientOverride in customRecipients)
            {
                foreach (var recipient in recipientOverride.Recipients)
                {
                    var missingNotificationDetailsError = ValidateNotificationChannelForRecipientOverrides(notification, recipient);
                    if (missingNotificationDetailsError != null)
                    {
                        return missingNotificationDetailsError;
                    }
                    var notificationInputError = ValidateContactInformationForRecipientOverrides(recipient);
                    if (notificationInputError != null)
                    {
                        return notificationInputError;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Validate that the recipient only provides organization number, social security number, or contact info in email or mobile number for the given notification channel.
        /// </summary>
        private Error? ValidateNotificationChannelForRecipientOverrides(NotificationRequest notification, Recipient recipient)
        {
            bool recipientHaveOrgOrSsn = !string.IsNullOrEmpty(recipient.OrganizationNumber) || !string.IsNullOrEmpty(recipient.NationalIdentityNumber);
            var reminderNotificationChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel;
            if (notification.NotificationChannel != NotificationChannel.Sms || reminderNotificationChannel != NotificationChannel.Sms)
            {
                if (recipient.EmailAddress is null && !recipientHaveOrgOrSsn)
                {
                    return NotificationErrors.MissingEmailRecipient;
                }
            }
            if (notification.NotificationChannel != NotificationChannel.Email || reminderNotificationChannel != NotificationChannel.Email)
            {
                if (recipient.MobileNumber is null && !recipientHaveOrgOrSsn)
                {
                    return NotificationErrors.MissingSmsRecipient;
                }
            }
            return null;
        }

        /// <summary>
        /// Validate that the recipient only provides organization number, social security number, or contact info in email or mobile number. If email or mobile number is provided, this is validated.
        /// </summary>
        private Error? ValidateContactInformationForRecipientOverrides(Recipient recipient)
        {
            bool recipientHasOrgNo = !string.IsNullOrEmpty(recipient.OrganizationNumber);
            bool recipientHasSsn = !string.IsNullOrEmpty(recipient.NationalIdentityNumber);
            bool recipientHasEmail = !string.IsNullOrEmpty(recipient.EmailAddress);
            bool recipientHasMobile = !string.IsNullOrEmpty(recipient.MobileNumber);
            if (recipientHasOrgNo)
            {
                if (recipientHasEmail || recipientHasMobile || recipientHasSsn)
                {
                    return NotificationErrors.OrgNumberWithSsnEmailOrMobile;
                }
            }
            if (recipientHasSsn)
            {
                if (recipientHasEmail || recipientHasMobile || recipientHasOrgNo)
                {
                    return NotificationErrors.SsnWithOrgNoEmailOrMobile;
                }
            }
            if (recipientHasEmail)
            {
                if (recipient.EmailAddress is not null && !emailRegex.IsMatch(recipient.EmailAddress))
                {
                    return NotificationErrors.InvalidEmailProvided;
                }
            }
            if (recipientHasMobile)
            {
                if (!mobileNumberHelper.IsValidMobileNumber(recipient.MobileNumber))
                {
                    return NotificationErrors.InvalidMobileNumberProvided;
                }
            }
            return null;
        }

        public CorrespondenceEntity MapToCorrespondenceEntity(InitializeCorrespondencesRequest request, string recipient, List<AttachmentEntity> attachmentsToBeUploaded, Guid partyUuid, bool isReserved)
        {
            List<CorrespondenceStatusEntity> statuses =
            [
                new CorrespondenceStatusEntity
                {
                    Status = CorrespondenceStatus.Initialized,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Initialized.ToString(),
                    PartyUuid = partyUuid
                },
            ];
            var currentStatus = GetCurrentCorrespondenceStatus(request.Correspondence, isReserved);
            if (currentStatus != CorrespondenceStatus.Initialized)
            {
                statuses.Add(new CorrespondenceStatusEntity
                {
                    Status = currentStatus,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = currentStatus.ToString(),
                    PartyUuid = partyUuid
                });
            }
            string sender = request.Correspondence.Sender;
            if (sender.StartsWith("0192:"))
            {
                sender = $"{UrnConstants.OrganizationNumberAttribute}:{request.Correspondence.Sender.WithoutPrefix()}";
                logger.LogInformation($"'0192:' prefix detected for sender in creation of correspondence. Replacing prefix with {UrnConstants.OrganizationNumberAttribute}.");
            }

            if (recipient.StartsWith("0192:"))
            {
                recipient = $"{UrnConstants.OrganizationNumberAttribute}:{recipient.WithoutPrefix()}";
                logger.LogInformation($"'0192:' prefix detected for recipient in creation of correspondence. Replacing prefix with {UrnConstants.OrganizationNumberAttribute}.");
            }
            else if (recipient.IsSocialSecurityNumber())
            {
                recipient = $"{UrnConstants.PersonIdAttribute}:{recipient}";
                logger.LogInformation($"Social security number without urn prefix detected for recipient in creation of correspondece. Adding {UrnConstants.PersonIdAttribute} prefix to recipient.");
            }

            return new CorrespondenceEntity
            {
                ResourceId = request.Correspondence.ResourceId,
                Recipient = recipient,
                Sender = sender,
                SendersReference = request.Correspondence.SendersReference,
                MessageSender = request.Correspondence.MessageSender,
                Content = new CorrespondenceContentEntity
                {
                    Attachments = attachmentsToBeUploaded.Select(a => new CorrespondenceAttachmentEntity
                    {
                        Attachment = a,
                        Created = DateTimeOffset.UtcNow,
                    }).ToList(),
                    Language = request.Correspondence.Content.Language,
                    MessageBody = request.Correspondence.Content.MessageBody,
                    MessageSummary = request.Correspondence.Content.MessageSummary,
                    MessageTitle = request.Correspondence.Content.MessageTitle,
                },
                RequestedPublishTime = request.Correspondence.RequestedPublishTime,
                AllowSystemDeleteAfter = request.Correspondence.AllowSystemDeleteAfter,
                DueDateTime = request.Correspondence.DueDateTime,
                PropertyList = request.Correspondence.PropertyList.ToDictionary(x => x.Key, x => x.Value),
                ReplyOptions = request.Correspondence.ReplyOptions,
                IgnoreReservation = request.Correspondence.IgnoreReservation,
                Statuses = statuses,
                Created = request.Correspondence.Created,
                ExternalReferences = request.Correspondence.ExternalReferences,
                Published = currentStatus == CorrespondenceStatus.Published ? DateTimeOffset.UtcNow : null,
                IsConfirmationNeeded = request.Correspondence.IsConfirmationNeeded,
            };
        }

        /// <summary>
        /// Validates the uploaded files. 
        /// Checks that the filename is valid, the files are the same as the attachments, and the files are not too large
        /// </summary>
        public Error? ValidateAttachmentFiles(List<IFormFile> files, List<CorrespondenceAttachmentEntity> attachments)
        {
            var maxUploadSize = long.Parse(int.MaxValue.ToString());
            foreach (var attachment in attachments)
            {
                if (attachment.Attachment?.DataLocationUrl != null) continue;

                var nameError = attachmentHelper.ValidateAttachmentName(attachment.Attachment!);
                if (nameError is not null) return nameError;

                var file = files.FirstOrDefault(a => a.FileName == attachment.Attachment?.FileName);
                if (file == null) return CorrespondenceErrors.UploadedFilesDoesNotMatchAttachments;
                if (file?.Length > maxUploadSize || file?.Length == 0) return AttachmentErrors.InvalidFileSize;
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
                var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true);
                if (attachment is not null)
                {
                    if (attachment.Sender.WithoutPrefix() != sender.WithoutPrefix()) return CorrespondenceErrors.InvalidSenderForAttachment;
                    attachments.Add(attachment);
                }
            }
            return attachments;
        }

        public CorrespondenceStatus GetCurrentCorrespondenceStatus(CorrespondenceEntity correspondence, bool isReserved)
        {
            if (isReserved && (correspondence.IgnoreReservation != true))
            {
                return CorrespondenceStatus.Reserved;
            }
            var status = correspondence.Statuses.LastOrDefault()?.Status ?? CorrespondenceStatus.Initialized;
            if (correspondence.Content.Attachments.All(c => c.Attachment?.Statuses != null && c.Attachment.StatusHasBeen(AttachmentStatus.Published)))
            {
                if (hostEnvironment.IsDevelopment() && correspondence.RequestedPublishTime < DateTimeOffset.UtcNow) status = CorrespondenceStatus.Published; // used to test on published correspondences in development
                else status = CorrespondenceStatus.ReadyForPublish;
            }
            return status;
        }

        public async Task<Error?> UploadAttachments(List<AttachmentEntity> correspondenceAttachments, List<IFormFile> files, Guid partyUuid, CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                var attachment = correspondenceAttachments.FirstOrDefault(a => a.FileName.ToLower() == file.FileName.ToLower());

                if (attachment == null)
                {
                    return CorrespondenceErrors.UploadedFilesDoesNotMatchAttachments;
                }
                OneOf<UploadAttachmentResponse, Error> uploadResponse;
                await using (var f = file.OpenReadStream())
                {
                    uploadResponse = await attachmentHelper.UploadAttachment(f, attachment.Id, partyUuid, cancellationToken);
                }
                var error = uploadResponse.Match(
                    _ => { return null; },
                    error => { return error; }
                );
                if (error != null) return error;
            }
            return null;
        }

        public async Task<AttachmentEntity> ProcessNewAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, Guid partyUuid, CancellationToken cancellationToken)
        {
            var status = new List<AttachmentStatusEntity>(){
                new AttachmentStatusEntity
                {
                    Status = AttachmentStatus.Initialized,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = AttachmentStatus.Initialized.ToString(),
                    PartyUuid = partyUuid
                }
            };
            var attachment = correspondenceAttachment.Attachment!;
            attachment.Statuses = status;
            if (attachment.Sender.StartsWith("0192:"))
            {
                attachment.Sender = $"{UrnConstants.OrganizationNumberAttribute}:{attachment.Sender.WithoutPrefix()}";
                logger.LogInformation($"'0192:' prefix detected for sender in initialization of attachment. Replacing prefix with {UrnConstants.OrganizationNumberAttribute}.");
            }
            return await attachmentRepository.InitializeAttachment(attachment, cancellationToken);
        }
    }
}