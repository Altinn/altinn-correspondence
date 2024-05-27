using System.Net;

namespace Altinn.Correspondence.Application;

public record Error(int ErrorCode, string Message, HttpStatusCode StatusCode);

public static class Errors
{
    public static Error AttachmentNotFound = new Error(1, "The requested attachment was not found", HttpStatusCode.NotFound);
    public static Error OffsetAndLimitIsNegative = new Error(2, "Limit and offset must be greater than or equal to 0", HttpStatusCode.BadRequest);

}
