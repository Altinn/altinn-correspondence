using System.Net;

namespace Altinn.Correspondence.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);
public static class CorrespondenceErrors
{
    public static Error CorrespondenceNotFound = new Error(1001, "The requested correspondence was not found", HttpStatusCode.NotFound);
    public static Error MessageTitleIsNotPlainText = new Error(1002, "Message title must be plain text", HttpStatusCode.BadRequest);
    public static Error MessageBodyIsNotMarkdown = new Error(1003, "Message body must be markdown", HttpStatusCode.BadRequest);
    public static Error MessageSummaryIsNotPlainText = new Error(1004, "Message summary must be plain text", HttpStatusCode.BadRequest);
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
    public static Error MessageBodyTooLong = new Error(1022, "Message body cannot exceed 30000 characters", HttpStatusCode.BadRequest);
    public static Error InvalidLanguage = new Error(1023, "Invalid language chosen. Supported languages is Norsk bokmål (nb), Nynorsk (nn) and English (en)", HttpStatusCode.BadRequest);
    public static Error ReadBeforeFetched = new Error(1024, "Correspondence must be fetched before it can be read", HttpStatusCode.BadRequest);
    public static Error ConfirmBeforeFetched = new Error(1025, "Correspondence must be fetched before it can be confirmed", HttpStatusCode.BadRequest);
    public static Error ArchiveBeforeConfirmed = new Error(1026, "Cannot archive or delete a correspondence which has not been confirmed when confirmation is required", HttpStatusCode.BadRequest);
    public static Error InvalidDateRange = new Error(1027, "From date cannot be after to date", HttpStatusCode.BadRequest);
    public static Error OffsetAndLimitIsNegative = new Error(1028, "Limit and offset must be greater than or equal to 0", HttpStatusCode.BadRequest);
    public static Error RecipientLookupFailed(List<string> recipients) { return new Error(1029, $"Could not find partyId for the following recipients: {string.Join(", ", recipients)}", HttpStatusCode.NotFound); }
    public static Error RecipientReserved(string recipientId) => new Error(1030, $"Recipient {recipientId} has reserved themselves from public correspondences. Can be overridden using the 'IgnoreReservation' flag.", HttpStatusCode.UnprocessableEntity);
    public static Error AttachmentNotAvailableForRecipient = new(1031, "Attachment is not available for recipient, latest status of correspondence is not in [Published, Fetched, Read, Replied, Confirmed, Archived, Reserved, AttachmentsDownloaded]", HttpStatusCode.BadRequest);
    public static Error ContactReservationRegistryFailed = new Error(1032, "Contact reservation registry lookup failed", HttpStatusCode.InternalServerError);
    public static Error InvalidIdempotencyKey = new Error(1033, "The idempotency key must be a valid non-empty GUID", HttpStatusCode.BadRequest);
    public static Error DuplicateInitCorrespondenceRequest = new Error(1034, "A correspondence with the same idempotent key already exists", HttpStatusCode.Conflict);
    public static Error InvalidReplyOptions = new Error(1035, "Reply options must be well-formed URIs and HTTPS with a max length of 255 characters", HttpStatusCode.BadRequest);
    public static Error InvalidResource = new Error(1036, "ResourceId must match an existing resource in the resource registry", HttpStatusCode.BadRequest);
    public static Error MessageTitleTooLong = new Error(1037, "Message title cannot exceed 255 characters", HttpStatusCode.BadRequest);
    public static Error AttachmentCountExceeded = new Error(1038, "A correspondence cannot contain more than 100 attachments in total", HttpStatusCode.BadRequest);
    public static Error MessageSenderIsNotPlainText = new Error(1039, "Message sender must be plain text", HttpStatusCode.BadRequest);
    public static Error AlreadyMarkedAsRead = new Error(1040, "Correspondence is already marked as read", HttpStatusCode.BadRequest);
    public static Error CorrespondenceAlreadyConfirmed = new Error(1041, "Correspondence has already been confirmed", HttpStatusCode.BadRequest);
    public static Error MessageSummaryWrongLength = new Error(1042, "Message summary, if not null, must be between 0 and 255 characters long", HttpStatusCode.BadRequest);
    public static Error CannotPurgeCorrespondenceLinkedToDialogportenTransmission = new Error(1043, "Cannot purge correspondence linked to a Dialogporten Transmission", HttpStatusCode.BadRequest);
    public static Error RecipientLacksRequiredRolesForCorrespondence(List<string> recipients) { return new Error(1044, $"The following recipients lack required roles to read the correspondence: {string.Join(", ", recipients)}", HttpStatusCode.BadRequest); }
    public static Error TransmissionOnlyAllowsOneRecipient = new Error(1045, "Transmission correspondences only support one recipient", HttpStatusCode.BadRequest);
    public static Error RecipientMismatch = new Error(1046, "The recipient of the correspondence must be equal to the party of the dialog of the transmission", HttpStatusCode.BadRequest);
    public static Error IdempotencyKeyNotAllowedWithMultipleRecipients = new Error(1047, "Idempotency key is not supported for requests with multiple recipients", HttpStatusCode.BadRequest);
    public static Error InvalidCorrespondenceDialogId = new Error(1048, "DialogId must be a valid non-empty GUID", HttpStatusCode.BadRequest);
    public static Error DialogNotFoundWithDialogId = new Error(1049, "Could not find dialog in Dialogporten with the given DialogId", HttpStatusCode.BadRequest);
    public static Error AttachmentExpirationTooSoonAfterRequestedPublishTime = new Error(1050, "The expiration time of attachments on the correspondence must be at least 14 days after the requested publish time of the correspondence", HttpStatusCode.BadRequest);
    public static Error TransmissionNotAllowedWithGuiActions = new Error(1051, "Correspondences with GUI actions (ReplyOptions or IsConfirmationNeeded) cannot be sent as transmissions", HttpStatusCode.BadRequest);
}

public static class AttachmentErrors
{
    public static Error ResourceRegistryLookupFailed = new Error(2000, "Resource registry lookup failed", HttpStatusCode.InternalServerError);
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
    public static Error FiletypeNotAllowed = new Error(2013, "Filetype not allowed", HttpStatusCode.BadRequest);
    public static Error ServiceOwnerNotFound = new Error(2014, "Service owner not setup in this environment. You need a service owner agreement to use Correspondence. Please contact us at Slack.", HttpStatusCode.UnavailableForLegalReasons);
    public static Error AttachmentAlreadyMigrated = new Error(2015, "Attachment has already been migrated", HttpStatusCode.Conflict);
    public static Error AttachedToAPublishedCorrespondence = new Error(2016, "This attachment is associated with a published correspondence and can no longer be accessed by service owner", HttpStatusCode.BadRequest);
    public static Error AttachmentExpirationPriorTwoWeeksFromNow = new Error(2017, "Attachment expirationTime must be at least 14 days from now", HttpStatusCode.BadRequest);
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
    public static Error InvalidEmailProvided = new Error(3011, "Invalid email provided for custom recipient.", HttpStatusCode.BadRequest);
    public static Error InvalidMobileNumberProvided = new Error(3012, "Invalid mobile number provided. Mobile number can contain only '+' and numeric characters, and it must adhere to the E.164 standard.", HttpStatusCode.BadRequest);
    public static Error CustomRecipientWithNumberOrEmailNotAllowedWithKeyWordRecipientName = new Error(3015, "Recipient overrides with email or mobile number are not allowed when using notification recipient name because of name lookup", HttpStatusCode.BadRequest);
    public static Error CustomRecipientWithMultipleRecipientsNotAllowed = new Error(3017, "Custom recipient with multiple recipients is not allowed", HttpStatusCode.BadRequest);
    public static Error CustomRecipientWithMultipleIdentifiersNotAllowed = new Error(3018, "Custom recipient with multiple identifiers is not allowed", HttpStatusCode.BadRequest);
    public static Error CustomRecipientWithoutIdentifierNotAllowed = new Error(3019, "Custom recipient without identifier is not allowed", HttpStatusCode.BadRequest);
    public static Error NotificationNotFound = new Error(3020, "Notification not found in the database", HttpStatusCode.NotFound);
    public static Error NotificationDetailsNotFound = new Error(3021, "Cannot retrieve notification details from Altinn Notification API", HttpStatusCode.NotFound);
    public static Error OverrideRegisteredContactInformationRequiresCustomRecipients = new Error(3022, "OverrideRegisteredContactInformation flag can only be used when CustomRecipients is provided", HttpStatusCode.BadRequest);
    public static Error InvalidNotificationTemplate = new Error(3023, "Invalid notification template", HttpStatusCode.BadRequest);
}
public static class AuthorizationErrors
{
    public static Error NoAccessToResource = new Error(4001, "You must use an Altinn token, DialogToken or log in to IDPorten as someone with access to the resource and organization in Altinn Authorization", HttpStatusCode.Unauthorized);
    public static Error CouldNotFindPartyUuid = new Error(4002, "Could not retrieve party uuid from lookup in Altinn Register", HttpStatusCode.BadRequest);
    public static Error InvalidPartyId = new Error(4003, "Invalid partyId", HttpStatusCode.BadRequest);
    public static Error LegacyNoAccessToCorrespondence = new Error(4004, "User does not have access to the correspondence", HttpStatusCode.Unauthorized);
    public static Error LegacyNotAccessToOwner(int partyId) { return new Error(4005, $"User does not have access to party with partyId {partyId}", HttpStatusCode.Unauthorized); }
    public static Error CouldNotDetermineCaller = new Error(4006, "Could not determine caller", HttpStatusCode.Unauthorized);
    public static Error CouldNotFindOrgNo = new Error(4007, "Could not identify orgnumber from user", HttpStatusCode.Unauthorized);
    public static Error IncorrectResourceType = new Error(4009, "Resource type is not supported. Resource must be of type GenericAccessResource or CorrespondenceService.", HttpStatusCode.BadRequest);
}

public static class SyncErrors
{   
    public static Error NoEventsToSync = new Error(5001, "No Events were specified in request", HttpStatusCode.BadRequest);
}

public static class StatisticsErrors
{
    public static Error NoCorrespondencesFound = new Error(6001, "No correspondences found for report generation", HttpStatusCode.NotFound);
    public static Error ReportGenerationFailed = new Error(6002, "Failed to generate statistics report", HttpStatusCode.InternalServerError);
}