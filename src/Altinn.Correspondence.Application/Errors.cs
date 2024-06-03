using Altinn.Correspondence.Application.UploadAttachmentCommand;
using OneOf;
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
    public static Error InvalidAttachmentStatus = new(6, "File has already been or is being uploaded", HttpStatusCode.BadRequest);
    public static Error CorrespondenceNotPublished = new Error(7, "A correspondence can only be confirmed or read when it is published. See correspondence status.", HttpStatusCode.BadRequest);
}
