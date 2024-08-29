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
    public static Error CorrespondenceNotPublished = new Error(8, "A correspondence can only be confirmed or read when it is published. See correspondence status.", HttpStatusCode.BadRequest);
    public static Error PurgeAttachmentWithExistingCorrespondence = new Error(9, "Attachment cannot be purged as it is linked to atleast one existing correspondence", HttpStatusCode.BadRequest);
    public static Error TooManyMessageBodies = new Error(10, "Only one attachment can be marked as message body", HttpStatusCode.BadRequest);
    public static Error NoMessageBody = new Error(11, "Atleast one attachment must be marked as message body", HttpStatusCode.BadRequest);
    public static Error NoAttachments = new Error(12, "Need atleast one attachment to upload", HttpStatusCode.BadRequest);
    public static Error CorrespondencePurged = new Error(13, "Correspondence has been purged", HttpStatusCode.BadRequest);
    public static Error CorrespondenceAlreadyPurged = new Error(14, "Correspondence has already been purged", HttpStatusCode.BadRequest);
    public static Error MessageTitleIsNotPlainText = new Error(15, "Message title must be plain text", HttpStatusCode.BadRequest);
    public static Error MessageBodyIsNotMarkdown = new Error(16, "Message body must be markdown", HttpStatusCode.BadRequest);
    public static Error MessageSummaryIsNotMarkdown = new Error(17, "Message summary must be markdown", HttpStatusCode.BadRequest);
    public static Error CorrespondenceHasNotBeenRead = new Error(18, "Correspondence has not been read", HttpStatusCode.BadRequest);
    public static Error NoAccessToResource = new Error(19, "You must use an Altinn token that represents a user with access to the resource in Altinn Authorization", HttpStatusCode.Unauthorized);
    public static Error UploadedFilesDoesNotMatchAttachments = new Error(20, "Mismatch between uploaded files and attachment metadata", HttpStatusCode.BadRequest);
    public static Error DuplicateRecipients = new Error(21, "Recipients must be unique", HttpStatusCode.BadRequest);
    public static Error UploadCorrespondenceNoAttachments = new Error(22, "When uploading correspondences, either upload or use existing attachments", HttpStatusCode.BadRequest);
    public static Error HashError = new Error(23, "Checksum mismatch", HttpStatusCode.BadRequest);
    public static Error DataLocationNotFound = new Error(24, "Could not get data location url", HttpStatusCode.BadRequest);
    public static Error ExistingAttachmentNotFound = new Error(25, "Existing attachment not found", HttpStatusCode.BadRequest);
    public static Error DueDatePriorToday = new Error(26, "Due date cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorToday = new Error(27, "AllowSystemDelete cannot be prior to today", HttpStatusCode.BadRequest);
    public static Error AllowSystemDeletePriorDueDate = new Error(28, "AllowSystemDelete cannot be prior to due date", HttpStatusCode.BadRequest);
}
