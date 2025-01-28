namespace Altinn.Correspondence.Common.Helpers
{
    public static class FileConstants
    {
        public static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".doc", "application/msword" },
            { ".xls", "application/vnd.ms-excel" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            { ".ppt", "application/vnd.ms-powerpoint" },
            { ".pps", "application/vnd.ms-powerpoint" },
            { ".zip", "application/zip" },
            { ".pdf", "application/pdf" },
            { ".html", "text/html" },
            { ".txt", "text/plain" },
            { ".xml", "text/xml" },
            { ".jpg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" },
            { ".png", "image/png" }
        };
        public static string GetMIMEType(string fileName)
        {
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            return MimeTypes.ContainsKey(fileExtension) ? MimeTypes[fileExtension] : "binary_octet_stream";
        }
    }
}