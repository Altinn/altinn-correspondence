using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.Settings;
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
using System.Text.RegularExpressions;

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
            var customRecipientError = ValidateCustomRecipient(notification, recipients);
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
        public Error? ValidateCustomRecipient(NotificationRequest notification, List<string> recipients)
        {
            var customRecipient = notification.CustomRecipient;

            // If no custom recipient is provided, no need to validate
            if (customRecipient == null)
            {
                return null;
            }
            
            // Validate that if the custom recipient exists, the correspondence does not have multiple recipients
            else
            {
                if (recipients.Count > 1)
                {
                    return NotificationErrors.CustomRecipientWithMultipleRecipientsNotAllowed;
                }
            }

            // Validate that the custom recipient only has one  and only one identifier
            var fieldsWithValue = new List<string>();
            if (!string.IsNullOrEmpty(customRecipient.OrganizationNumber)) fieldsWithValue.Add("OrganizationNumber");
            if (!string.IsNullOrEmpty(customRecipient.NationalIdentityNumber)) fieldsWithValue.Add("NationalIdentityNumber");
            if (!string.IsNullOrEmpty(customRecipient.EmailAddress)) fieldsWithValue.Add("EmailAddress");
            if (!string.IsNullOrEmpty(customRecipient.MobileNumber)) fieldsWithValue.Add("MobileNumber");

            if (fieldsWithValue.Count == 0)
            {
                return NotificationErrors.CustomRecipientWithoutIdentifierNotAllowed;
            }
            else if (fieldsWithValue.Count > 1)
            {
                return NotificationErrors.CustomRecipientWithMultipleIdentifiersNotAllowed;
            }

            // Validate that the custom recipient does not contain the keyword $recipientName$ if it has a number or email
            if (customRecipient.EmailAddress != null || customRecipient.MobileNumber != null)
            {
                if (TextContainsTag(notification.EmailBody, "$recipientName$") || TextContainsTag(notification.SmsBody, "$recipientName$")
                    || TextContainsTag(notification.EmailSubject, "$recipientName$") || TextContainsTag(notification.ReminderEmailBody, "$recipientName$")
                    || TextContainsTag(notification.ReminderSmsBody, "$recipientName$") || TextContainsTag(notification.ReminderEmailSubject, "$recipientName$"))
                {
                    return NotificationErrors.CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName;
                }
            }

            // Validate that the email address is valid
            if (customRecipient.EmailAddress is not null && !emailRegex.IsMatch(customRecipient.EmailAddress))
            {
                return NotificationErrors.InvalidEmailProvided;
            }

            // Validate that the mobile number is valid
            if (customRecipient.MobileNumber is not null && !mobileNumberHelper.IsValidMobileNumber(customRecipient.MobileNumber))
            {
                return NotificationErrors.InvalidMobileNumberProvided;
            }

            return null;
        }
        private bool TextContainsTag(string? text, string tag)
        {
            if (text == null) return false;
            return text.Contains(tag, StringComparison.CurrentCultureIgnoreCase);
        }

        public CorrespondenceEntity MapToCorrespondenceEntity(InitializeCorrespondencesRequest request, string recipient, List<AttachmentEntity> attachmentsToBeUploaded, Guid partyUuid, Party? partyDetails, bool isReserved, string serviceOwnerOrgNumber)
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

            if (recipient.IsWithISO6523Prefix())
            {
                logger.LogInformation($"'0192:' prefix detected for recipient in creation of correspondence. Replacing prefix with {UrnConstants.OrganizationNumberAttribute}.");
            }
            else if (recipient.IsSocialSecurityNumberWithNoPrefix())
            {
                logger.LogInformation($"Social security number without urn prefix detected for recipient in creation of correspondece. Adding {UrnConstants.PersonIdAttribute} prefix to recipient.");
            }
            recipient = recipient.WithoutPrefix().WithUrnPrefix();

            var sender = serviceOwnerOrgNumber.WithoutPrefix().WithUrnPrefix();

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
                    MessageBody = AddRecipientToMessage(request.Correspondence.Content.MessageBody, partyDetails?.Name),
                    MessageSummary = AddRecipientToMessage(request.Correspondence.Content.MessageSummary, partyDetails?.Name),
                    MessageTitle = AddRecipientToMessage(request.Correspondence.Content.MessageTitle, partyDetails?.Name),
                },
                RequestedPublishTime = request.Correspondence.RequestedPublishTime,
                AllowSystemDeleteAfter = request.Correspondence.AllowSystemDeleteAfter,
                DueDateTime = request.Correspondence.DueDateTime,
                PropertyList = request.Correspondence.PropertyList.ToDictionary(x => x.Key, x => x.Value),
                ReplyOptions = request.Correspondence.ReplyOptions,
                IgnoreReservation = request.Correspondence.IgnoreReservation,
                Statuses = statuses,
                Created = DateTime.UtcNow,
                ExternalReferences = request.Correspondence.ExternalReferences,
                Published = currentStatus == CorrespondenceStatus.Published ? DateTimeOffset.UtcNow : null,
                IsConfirmationNeeded = request.Correspondence.IsConfirmationNeeded,
                IsConfidential = request.Correspondence.IsConfidential,
                OriginalRequest = request.Correspondence.OriginalRequest,
            };
        }

        public string AddRecipientToMessage(string message, string recipient)
        {
            if (message.Contains("{{recipientName}}"))
            {
                return message.Replace("{{recipientName}}", recipient);
            }
            return message;
        }

        /// <summary>
        /// Validates the uploaded files. 
        /// Checks that the filename is valid, the files are the same as the attachments, and the files are not too large
        /// </summary>
        public Error? ValidateAttachmentFiles(List<IFormFile> files, List<CorrespondenceAttachmentEntity> attachments)
        {
            foreach (var attachment in attachments)
            {
                if (attachment.Attachment?.DataLocationUrl != null) continue;

                var nameError = attachmentHelper.ValidateAttachmentName(attachment.Attachment!);
                if (nameError is not null) return nameError;

                var file = files.FirstOrDefault(a => a.FileName == attachment.Attachment?.FileName);
                if (file == null) return CorrespondenceErrors.UploadedFilesDoesNotMatchAttachments;
                if (file?.Length > ApplicationConstants.MaxFileUploadSize || file?.Length == 0) return AttachmentErrors.InvalidFileSize;
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
                status = CorrespondenceStatus.ReadyForPublish;
            }
            return status;
        }

        public async Task<Error?> UploadAttachments(List<AttachmentEntity> correspondenceAttachments, List<IFormFile> files, Guid partyUuid, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Uploading {correspondenceAttachments.Count} correspondence attachments.");
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
                    uploadResponse = await attachmentHelper.UploadAttachment(f, attachment.Id, partyUuid, forMigration: false, cancellationToken);
                }
                var error = uploadResponse.Match(
                    _ => { return null; },
                    error => { return error; }
                );
                if (error != null) return error;
            }
            logger.LogInformation($"Uploaded {correspondenceAttachments.Count} correspondence attachments.");
            return null;
        }

        public async Task<AttachmentEntity> ProcessNewAttachment(CorrespondenceAttachmentEntity correspondenceAttachment, Guid partyUuid, string serviceOwnerOrgNumber, CancellationToken cancellationToken)
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
            
            // Set the Sender from the service owner organization number
            var sender = serviceOwnerOrgNumber.WithoutPrefix().WithUrnPrefix();
            attachment.Sender = sender;
            
            return await attachmentRepository.InitializeAttachment(attachment, cancellationToken);
        }

        public Error? ValidateReplyOptions(List<CorrespondenceReplyOptionEntity> replyOptions)
        {
            if (replyOptions == null)
            {
                return null;
            }
            foreach (var replyOption in replyOptions)
            {
                if (replyOption.LinkURL.Length > 255)
                {
                    return CorrespondenceErrors.InvalidReplyOptions;
                }
                if (!Uri.IsWellFormedUriString((string)replyOption.LinkURL, UriKind.Absolute))
                {
                    return CorrespondenceErrors.InvalidReplyOptions;
                }
                if (!replyOption.LinkURL.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return CorrespondenceErrors.InvalidReplyOptions;
                }
            }
            return null;
        }
    }
}