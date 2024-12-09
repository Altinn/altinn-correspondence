using System.Net;

namespace Altinn.Correspondence.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);
public static class CorrespondenceErrors
{
    public static Error CorrespondenceNotFound = new Error(1001, "The requested correspondence was not found", HttpStatusCode.NotFound);
    public static Error MessageTitleIsNotPlainText = new Error(1002, "Message title must be plain text", HttpStatusCode.BadRequest);
    public static Error MessageBodyIsNotMarkdown = new Error(1003, "Message body must be markdown", HttpStatusCode.BadRequest);
    public static Error MessageSummaryIsNotMarkdown = new Error(1004, "Message summary must be markdown", HttpStatusCode.BadRequest);
    public static Error UploadedFilesDoesNotMatchAttachments = new Error(1005, "Mismatch between uploaded files and attachment metadata", HttpStatusCode.BadRequest);
    public static Error DuplicateRecipients = new Error(1006, "Recipients must be unique", HttpStatusCode.BadRequest);
    public static Error ExistingAttachmentNotFound = new Error(1007, "Existing attachment not found", HttpStatusCode.BadRequest);
    public static Error DueDatePriorToday = new Error(1008, "DueDateTime cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error DueDatePriorRequestedPublishTime = new Error(1009, "DueDateTime cannot be prior to RequestedPublishTime", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorToday = new Error(1010, "AllowSystemDelete cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorRequestedPublishTime = new Error(1011, "AllowSystemDelete cannot be prior to RequestedPublishTime", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorDueDate = new Error(1012, "AllowSystemDelete cannot be prior to DueDateTime", HttpStatusCode.BadRequest);
    public static Error CantPurgePublishedCorrespondence = new Error(1013, "Sender cannot delete correspondence that has been published", HttpStatusCode.BadRequest);
    public static Error CorrespondenceAlreadyPurged = new Error(1014, "Correspondence has already been purged", HttpStatusCode.BadRequest);
    public static Error CouldNotRetrieveStatus = new Error(1015, "Could not retrieve highest status for correspondence", HttpStatusCode.BadRequest);
    public static Error DueDateRequired = new Error(1016, "DueDateTime is required when confirmation is needed", HttpStatusCode.BadRequest);
    public static Error InvalidSenderForAttachment = new Error(1017, "The sender of the correspondence must be equal the sender of existing attachments", HttpStatusCode.BadRequest);
    public static Error AttachmentsNotPublished = new Error(1018, "Attachment is not published", HttpStatusCode.BadRequest);
    public static Error MissingContent = new Error(1019, "The Content field must be provided for the correspondence", HttpStatusCode.BadRequest);
    public static Error MessageTitleEmpty = new Error(1020, "Message title cannot be empty", HttpStatusCode.BadRequest);
    public static Error MessageBodyEmpty = new Error(1021, "Message body cannot be empty", HttpStatusCode.BadRequest);
    public static Error MessageSummaryEmpty = new Error(1022, "Message summary cannot be empty", HttpStatusCode.BadRequest);
    public static Error InvalidLanguage = new Error(1023, "Invalid language chosen. Supported languages is Norsk bokmål (nb), Nynorsk (nn) and English (en)", HttpStatusCode.BadRequest);
    public static Error ReadBeforeFetched = new Error(1024, "Correspondence must be fetched before it can be read", HttpStatusCode.BadRequest);
    public static Error ConfirmBeforeFetched = new Error(1025, "Correspondence must be fetched before it can be confirmed", HttpStatusCode.BadRequest);
    public static Error ArchiveBeforeConfirmed = new Error(1026, "Cannot archive or delete a correspondence which has not been confirmed when confirmation is required", HttpStatusCode.BadRequest);
    public static Error InvalidDateRange = new Error(1027, "From date cannot be after to date", HttpStatusCode.BadRequest);
    public static Error OffsetAndLimitIsNegative = new Error(1028, "Limit and offset must be greater than or equal to 0", HttpStatusCode.BadRequest);
}
public static class AttachmentErrors
{
    public static Error AttachmentNotFound = new Error(2001, "The requested attachment was not found", HttpStatusCode.NotFound);
    public static Error UploadFailed = new Error(2002, "Error occurred during upload", HttpStatusCode.BadGateway);
    public static Error CantUploadToExistingCorrespondence = new Error(2003, "Cannot upload attachment to a correspondence that has been created", HttpStatusCode.BadRequest);
    public static Error InvalidFileSize = new Error(2004, "File must have content and has a max file size of 2GB", HttpStatusCode.BadRequest);
    public static Error FileAlreadyUploaded = new Error(2005, "File has already been or is being uploaded", HttpStatusCode.BadRequest);
    public static Error FileHasBeenPurged = new Error(2006, "File has already been purged", HttpStatusCode.BadRequest);
    public static Error PurgeAttachmentWithExistingCorrespondence = new Error(2007, "Attachment cannot be purged as it is linked to at least one existing correspondence", HttpStatusCode.BadRequest);
    public static Error HashMismatch = new Error(2008, "Checksum mismatch", HttpStatusCode.BadRequest);
    public static Error DataLocationNotFound = new Error(2009, "Could not get data location url", HttpStatusCode.BadRequest);
    public static Error FilenameMissing = new Error(2010, "Filename is missing", HttpStatusCode.BadRequest);
    public static Error FilenameTooLong = new Error(2011, "Filename is too long", HttpStatusCode.BadRequest);
    public static Error FilenameInvalid = new Error(2012, "Filename contains invalid characters", HttpStatusCode.BadRequest);
}
public static class NotificationErrors
{
    public static Error TemplateNotFound = new Error(3001, "The requested notification template with the given language was not found", HttpStatusCode.NotFound);
    public static Error MissingEmailContent = new Error(3002, "Email body and subject must be provided when sending email notifications", HttpStatusCode.BadRequest);
    public static Error MissingEmailReminderContent = new Error(3003, "Reminder email body and subject must be provided when sending reminder email notifications", HttpStatusCode.BadRequest);
    public static Error MissingSmsContent = new Error(3004, "SMS body must be provided when sending SMS notifications", HttpStatusCode.BadRequest);
    public static Error MissingSmsReminderContent = new Error(3005, "Reminder SMS body must be provided when sending reminder SMS notifications", HttpStatusCode.BadRequest);
    public static Error MissingPreferredChannel = new Error(3006, "Email body, subject and SMS body must be provided when sending preferred notifications", HttpStatusCode.BadRequest);
    public static Error MissingPreferredReminderChannel = new Error(3007, "Reminder email body, subject and SMS body must be provided when sending reminder preferred notifications", HttpStatusCode.BadRequest);
    public static Error CouldNotFindRecipientToOverride(string id) { return new Error(3008, $"Could not find recipient with id: {id} to override", HttpStatusCode.BadRequest); }
    public static Error MissingEmailRecipient = new Error(3009, "Missing email information for custom recipient. Add email or use the OrganizationNumber or NationalIdentityNumber fields for contact information", HttpStatusCode.BadRequest);
    public static Error MissingSmsRecipient = new Error(3010, "Missing mobile number for custom recipient. Add mobile number or use the OrganizationNumber or NationalIdentityNumber fields for contact information", HttpStatusCode.BadRequest);
    public static Error InvalidEmailProvided = new Error(3011, "Invalid email provided for custom recipient.", HttpStatusCode.BadRequest);
    public static Error InvalidMobileNumberProvided = new Error(3012, "Invalid mobile number provided. Mobile number can contain only '+' and numeric characters, and it must adhere to the E.164 standard.", HttpStatusCode.BadRequest);
    public static Error OrgNumberWithSsnEmailOrMobile = new Error(3013, "Organization number cannot be combined with email address, mobile number, or national identity number.", HttpStatusCode.BadRequest);
    public static Error SsnWithOrgNoEmailOrMobile = new Error(3014, "National identity number cannot be combined with email address, mobile number, or organization number.", HttpStatusCode.BadRequest);
}
public static class AuthorizationErrors
{
    public static Error NoAccessToResource = new Error(4001, "You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and orgaization in Altinn Authorization", HttpStatusCode.Unauthorized);
    public static Error CouldNotFindPartyUuid = new Error(4002, "Could not retrieve party uuid from lookup in Altinn Register", HttpStatusCode.BadRequest);
    public static Error InvalidPartyId = new Error(4003, "Invalid partyId", HttpStatusCode.BadRequest);
    public static Error LegacyNoAccessToCorrespondence = new Error(4004, "User does not have access to the correspondence", HttpStatusCode.Unauthorized);
    public static Error LegacyNotAccessToOwner(int partyId) { return new Error(4005, $"User does not have access to party with partyId {partyId}", HttpStatusCode.Unauthorized); }
    public static Error CouldNotDetermineCaller = new Error(4006, "Could not determine caller", HttpStatusCode.Unauthorized);
    public static Error CouldNotFindOrgNo = new Error(4007, "Could not identify orgnumber from user", HttpStatusCode.Unauthorized);
}
