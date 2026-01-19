using Microsoft.Net.Http.Headers;
using System.Net.Mime;

namespace Altinn.Correspondence.API.Helpers;

public static class FileDownloadResponseHelper
{
    public static string GetContentTypeFromFileName(string? fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".txt" => "text/plain; charset=utf-8",
            ".csv" => "text/csv; charset=utf-8",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html; charset=utf-8",
            _ => MediaTypeNames.Application.Octet
        };
    }

    public static void SetInlineContentDisposition(HttpResponse response, string fileName)
    {
        var contentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = fileName
        };

        response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
    }
}
