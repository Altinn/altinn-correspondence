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
    public static Error NoAttachments = new Error(12, "Need atleast one attachments, which must be marked as message body", HttpStatusCode.BadRequest);
    public static Error CorrespondencePurged = new Error(13, "Correspondence has been purged", HttpStatusCode.BadRequest);
}
