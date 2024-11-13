using System.Net;

namespace Altinn.Correspondence.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

public static class Errors
{
    public static Error CorrespondenceNotFound = new Error(1, "The requested correspondence was not found", HttpStatusCode.NotFound);
    public static Error AttachmentNotFound = new Error(2, "The requested attachment was not found", HttpStatusCode.NotFound);
    public static Error OffsetAndLimitIsNegative = new Error(3, "Limit and offset must be greater than or equal to 0", HttpStatusCode.BadRequest);
    public static Error UploadFailed = new Error(4, "Error occurred during upload", HttpStatusCode.BadGateway);
    public static Error InvalidFileSize = new Error(5, "File must have content and has a max file size of 2GB", HttpStatusCode.BadRequest);
    public static Error InvalidUploadAttachmentStatus = new Error(6, "File has already been or is being uploaded", HttpStatusCode.BadRequest);
    public static Error InvalidPurgeAttachmentStatus = new Error(7, "File has already been purged", HttpStatusCode.BadRequest);
    public static Error PurgeAttachmentWithExistingCorrespondence = new Error(8, "Attachment cannot be purged as it is linked to at least one existing correspondence", HttpStatusCode.BadRequest);
    public static Error NoAttachments = new Error(9, "For upload requests at least one attachment has to be included", HttpStatusCode.BadRequest);
    public static Error CorrespondencePurged = new Error(10, "Correspondence has been purged", HttpStatusCode.BadRequest);
    public static Error CorrespondenceAlreadyPurged = new Error(11, "Correspondence has already been purged", HttpStatusCode.BadRequest);
    public static Error MessageTitleIsNotPlainText = new Error(12, "Message title must be plain text", HttpStatusCode.BadRequest);
    public static Error MessageBodyIsNotMarkdown = new Error(13, "Message body must be markdown", HttpStatusCode.BadRequest);
    public static Error MessageSummaryIsNotMarkdown = new Error(14, "Message summary must be markdown", HttpStatusCode.BadRequest);
    public static Error CorrespondenceHasNotBeenRead = new Error(15, "Correspondence has not been read", HttpStatusCode.BadRequest);
    public static Error NoAccessToResource = new Error(16, "You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization", HttpStatusCode.Unauthorized);
    public static Error UploadedFilesDoesNotMatchAttachments = new Error(17, "Mismatch between uploaded files and attachment metadata", HttpStatusCode.BadRequest);
    public static Error DuplicateRecipients = new Error(18, "Recipients must be unique", HttpStatusCode.BadRequest);
    public static Error HashError = new Error(19, "Checksum mismatch", HttpStatusCode.BadRequest);
    public static Error DataLocationNotFound = new Error(20, "Could not get data location url", HttpStatusCode.BadRequest);
    public static Error ExistingAttachmentNotFound = new Error(21, "Existing attachment not found", HttpStatusCode.BadRequest);
    public static Error DueDatePriorToday = new Error(22, "DueDateTime cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error DueDatePriorRequestedPublishTime = new Error(23, "DueDateTime cannot be prior to RequestedPublishTime", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorToday = new Error(24, "AllowSystemDelete cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorRequestedPublishTime = new Error(25, "AllowSystemDelete cannot be prior to RequestedPublishTime", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorDueDate = new Error(26, "AllowSystemDelete cannot be prior to DueDateTime", HttpStatusCode.BadRequest);
    public static Error CouldNotFindOrgNo = new Error(27, "Could not identify orgnumber from user", HttpStatusCode.Unauthorized);
    public static Error CantPurgeCorrespondenceSender = new Error(28, "Cannot delete correspondence that has been published", HttpStatusCode.BadRequest);
    public static Error CantUploadToExistingCorrespondence = new Error(29, "Cannot upload attachment to a correspondence that has been created", HttpStatusCode.BadRequest);
    public static Error LatestStatusIsNull = new Error(30, "Could not retrieve latest status for correspondence", HttpStatusCode.BadRequest);
    public static Error InvalidSender = new Error(31, "Creator of correspondence must be the sender", HttpStatusCode.BadRequest);
    public static Error NotificationTemplateNotFound = new Error(32, "The requested notification template with the given language was not found", HttpStatusCode.NotFound);
    public static Error MissingEmailContent = new Error(33, "Email body and subject must be provided when sending email notifications", HttpStatusCode.BadRequest);
    public static Error MissingEmailReminderNotificationContent = new Error(34, "Reminder email body and subject must be provided when sending reminder email notifications", HttpStatusCode.BadRequest);
    public static Error MissingSmsContent = new Error(35, "SMS body must be provided when sending SMS notifications", HttpStatusCode.BadRequest);
    public static Error MissingSmsReminderNotificationContent = new Error(36, "Reminder SMS body must be provided when sending reminder SMS notifications", HttpStatusCode.BadRequest);
    public static Error MissingPrefferedNotificationContent = new Error(37, "Email body, subject and SMS body must be provided when sending preferred notifications", HttpStatusCode.BadRequest);
    public static Error MissingPrefferedReminderNotificationContent = new Error(38, $"Reminder email body, subject and SMS body must be provided when sending reminder preferred notifications", HttpStatusCode.BadRequest);
    public static Error AttachmentNotPublished = new Error(39, "Attachment is not published", HttpStatusCode.BadRequest);
    public static Error LegacyNotAccessToOwner(int partyId) { return new Error(40, $"User does not have access to party with partyId {partyId}", HttpStatusCode.Unauthorized); }
    public static Error ArchiveBeforeConfirmed = new Error(41, "Cannot archive or delete a correspondence which has not been confirmed when confirmation is required", HttpStatusCode.BadRequest);
    public static Error DueDateRequired = new Error(42, "DueDateTime is required when confirmation is needed", HttpStatusCode.BadRequest);
    public static Error MissingContent = new Error(43, "The Content field must be provided for the correspondence", HttpStatusCode.BadRequest);
    public static Error MessageTitleEmpty = new Error(44, "Message title cannot be empty", HttpStatusCode.BadRequest);
    public static Error MessageBodyEmpty = new Error(45, "Message body cannot be empty", HttpStatusCode.BadRequest);
    public static Error MessageSummaryEmpty = new Error(46, "Message summary cannot be empty", HttpStatusCode.BadRequest);
    public static Error InvalidLanguage = new Error(47, "Invalid language chosen. Supported languages is Norsk bokmål (nb), Nynorsk (nn) and English (en)", HttpStatusCode.BadRequest);
    public static Error LegacyNoAccessToCorrespondence = new Error(48, "User does not have access to the correspondence", HttpStatusCode.Unauthorized);
    public static Error InvalidPartyId = new Error(49, "Invalid partyId", HttpStatusCode.BadRequest);
    public static Error ConfirmBeforeFetched = new Error(50, "Correspondence must be fetched before it can be confirmed", HttpStatusCode.BadRequest);
    public static Error ReadBeforeFetched = new Error(51, "Correspondence must be fetched before it can be read", HttpStatusCode.BadRequest);
    public static Error InvalidDateRange = new Error(52, "From date cannot be after to date", HttpStatusCode.BadRequest);
}
