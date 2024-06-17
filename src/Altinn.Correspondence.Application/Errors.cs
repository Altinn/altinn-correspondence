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
    public static Error AttachmentNotPublished = new Error(10, "Attachment has not been published yet", HttpStatusCode.BadRequest);
    public static Error CorrespondenceNotOpenForAttachments = new Error(11, "Attachments cannot be changed after a correspondence has been published", HttpStatusCode.BadRequest);
    public static Error CorrespondenceAttachmentNotFound = new Error(12, "Could not find an attachment with the given ID on the correspondence", HttpStatusCode.NotFound);
}
