using System.Net;

namespace Altinn.Correspondence.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

public static class Errors
{
    public static Error AttachmentNotFound = new Error(1, "The requested attachment was not found", HttpStatusCode.NotFound);
    public static Error OffsetAndLimitIsNegative = new Error(2, "Limit and offset must be greater than or equal to 0", HttpStatusCode.BadRequest);
    public static Error CorrespondenceNotFound = new Error(3, "The requested correspondence was not found", HttpStatusCode.NotFound);
    public static Error CorrespondenceNotPublished = new Error(4, "A correspondence can only be confirmed or read when it is published. See correspondence status.", HttpStatusCode.BadRequest);

}
