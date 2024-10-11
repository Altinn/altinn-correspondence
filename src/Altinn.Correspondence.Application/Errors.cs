using System.Net;

namespace Altinn.Correspondence.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

public static class Errors
{
    public static Error CorrespondenceNotFound = new Error(1, "The requested correspondence was not found", HttpStatusCode.NotFound);
    public static Error AttachmentNotFound = new(2, "The requested attachment was not found", HttpStatusCode.NotFound);
    public static Error OffsetAndLimitIsNegative = new(3, "Limit and offset must be greater than or equal to 0", HttpStatusCode.BadRequest);
    public static Error UploadFailed = new(4, "Error occurred during upload", HttpStatusCode.BadGateway);
    public static Error InvalidFileSize = new(5, "File must have content and has a max file size of 2GB", HttpStatusCode.BadRequest);
    public static Error InvalidUploadAttachmentStatus = new(6, "File has already been or is being uploaded", HttpStatusCode.BadRequest);
    public static Error InvalidPurgeAttachmentStatus = new(7, "File has already been purged", HttpStatusCode.BadRequest);
    public static Error PurgeAttachmentWithExistingCorrespondence = new Error(8, "Attachment cannot be purged as it is linked to at least one existing correspondence", HttpStatusCode.BadRequest);
    public static Error TooManyMessageBodies = new Error(9, "Only one attachment can be marked as message body", HttpStatusCode.BadRequest);
    public static Error NoMessageBody = new Error(10, "At least one attachment must be marked as message body", HttpStatusCode.BadRequest);
    public static Error NoAttachments = new Error(11, "For upload requests at least one attachment has to be included", HttpStatusCode.BadRequest);
    public static Error CorrespondencePurged = new Error(12, "Correspondence has been purged", HttpStatusCode.BadRequest);
    public static Error CorrespondenceAlreadyPurged = new Error(13, "Correspondence has already been purged", HttpStatusCode.BadRequest);
    public static Error MessageTitleIsNotPlainText = new Error(14, "Message title must be plain text", HttpStatusCode.BadRequest);
    public static Error MessageBodyIsNotMarkdown = new Error(15, "Message body must be markdown", HttpStatusCode.BadRequest);
    public static Error MessageSummaryIsNotMarkdown = new Error(16, "Message summary must be markdown", HttpStatusCode.BadRequest);
    public static Error CorrespondenceHasNotBeenRead = new Error(17, "Correspondence has not been read", HttpStatusCode.BadRequest);
    public static Error NoAccessToResource = new Error(18, "You must use an Altinn token that represents a user with access to the resource in Altinn Authorization", HttpStatusCode.Unauthorized);
    public static Error UploadedFilesDoesNotMatchAttachments = new Error(19, "Mismatch between uploaded files and attachment metadata", HttpStatusCode.BadRequest);
    public static Error DuplicateRecipients = new Error(20, "Recipients must be unique", HttpStatusCode.BadRequest);
    public static Error UploadCorrespondenceNoAttachments = new Error(21, "When uploading correspondences, either upload or use existing attachments", HttpStatusCode.BadRequest);
    public static Error HashError = new Error(22, "Checksum mismatch", HttpStatusCode.BadRequest);
    public static Error DataLocationNotFound = new Error(23, "Could not get data location url", HttpStatusCode.BadRequest);
    public static Error ExistingAttachmentNotFound = new Error(24, "Existing attachment not found", HttpStatusCode.BadRequest);
    public static Error DueDatePriorToday = new Error(25, "DueDateTime cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error DueDatePriorRequestedPublishTime = new Error(26, "DueDateTime cannot be prior to RequestedPublishTime", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorToday = new Error(27, "AllowSystemDelete cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorRequestedPublishTime = new Error(28, "AllowSystemDelete cannot be prior to RequestedPublishTime", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorDueDate = new Error(29, "AllowSystemDelete cannot be prior to DueDateTime", HttpStatusCode.BadRequest);
    public static Error CouldNotFindOrgNo = new Error(30, "Could not identify orgnumber from user", HttpStatusCode.Unauthorized);
    public static Error CantPurgeCorrespondenceSender = new Error(31, "Cannot delete correspondence that has been published", HttpStatusCode.BadRequest);
    public static Error CantUploadToExistingCorrespondence = new Error(32, "Cannot upload attachment to a correspondence that has been created", HttpStatusCode.BadRequest);
    public static Error CorrespondenceFailedDuringUpload = new Error(33, "Correspondence status failed during uploading of attachment", HttpStatusCode.BadRequest);
    public static Error LatestStatusIsNull = new Error(34, "Could not retrieve latest status for correspondence", HttpStatusCode.BadRequest);
    public static Error InvalidSender = new Error(35, "Creator of correspondence must be the sender", HttpStatusCode.BadRequest);
    public static Error CorrespondenceDoesNotHaveNotifications = new Error(36, "The Correspondence does not have any connected notifications", HttpStatusCode.BadRequest);
    public static Error NotificationTemplateNotFound = new Error(37, "The requested notification template with the given language was not found", HttpStatusCode.NotFound);
    public static Error MissingEmailContent = new Error(38, "Email body and subject must be provided when sending email notifications", HttpStatusCode.BadRequest);
    public static Error MissingEmailReminderNotificationContent = new Error(39, "Reminder email body and subject must be provided when sending reminder email notifications", HttpStatusCode.BadRequest);
    public static Error MissingSmsContent = new Error(40, "SMS body must be provided when sending SMS notifications", HttpStatusCode.BadRequest);
    public static Error MissingSmsReminderNotificationContent = new Error(41, "Reminder SMS body must be provided when sending reminder SMS notifications", HttpStatusCode.BadRequest);
    public static Error MissingPrefferedNotificationContent = new Error(42, "Email body, subject and SMS body must be provided when sending preferred notifications", HttpStatusCode.BadRequest);
    public static Error MissingPrefferedReminderNotificationContent = new Error(43, $"Reminder email body, subject and SMS body must be provided when sending reminder preferred notifications", HttpStatusCode.BadRequest);
    public static Error NoExistingAttachments = new Error(44, "Initializing correspondence without upload requires existing attachments", HttpStatusCode.BadRequest);
    public static Error AttachmentNotPublished = new Error(45, "Attachment is not published", HttpStatusCode.BadRequest);
}
