namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers;

internal static class DialogportenAttachmentMediaTypeMapper
{
    internal static string? GetDialogportenAttachmentMediaTypeForFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".json" => "application/json",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",

            // Supported by correspondence, but not in Dialogporten's attachment media type list
            ".ppt" => "PPT",
            ".pps" => "PPS",
            ".gif" => "GIF",
            ".bmp" => "BMP",

            _ => null
        };
    }
}

