using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Altinn.Correspondence.API.Helpers;

/// <summary>
/// An <see cref="IActionResult"/> that streams content directly to the HTTP response body via a callback. Used for the
/// "download all attachments" zip download.
/// </summary>
/// <remarks>
/// Once the callback starts writing to the response body the status code and headers are already
/// committed, so the callback cannot signal an error via the response. All validation that can fail
/// must happen before this result is returned.
/// </remarks>
public sealed class FileCallbackResult(string contentType, string fileDownloadName, long? contentLength, Func<Stream, CancellationToken, Task> callback) : ActionResult
{
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = contentType;
        response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = fileDownloadName
        }.ToString();

        if (contentLength.HasValue)
        {
            response.ContentLength = contentLength.Value;
        }

        // Stream out as produced rather than buffering the whole payload.
        context.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        //.NET 10's ZipArchive still flushes a small per-entry data descriptor (~16 bytes)
        // synchronously when an entry is closed
        var bodyControl = context.HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (bodyControl is not null)
        {
            bodyControl.AllowSynchronousIO = true;
        }

        await callback(response.Body, context.HttpContext.RequestAborted);
    }
}
