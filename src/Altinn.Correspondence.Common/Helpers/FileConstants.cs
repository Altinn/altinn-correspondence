namespace Altinn.Correspondence.Common.Helpers
{
    public static class FileConstants
    {
        public static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".doc", "application_msword" },
            { ".xls", "application_vnd_ms_excel" },
            { ".docx", "application_msword" },
            { ".xlsx", "application_vnd_ms_excel" },
            { ".ppt", "application_vnd_ms_powerpoint" },
            { ".pps", "application_vnd_ms_powerpoint" },
            { ".zip", "application_zip" },
            { ".pdf", "application_pdf" },
            { ".html", "text_html" },
            { ".txt", "text_plain" },
            { ".xml", "text_xml" },
            { ".jpg", "image_jpeg" },
            { ".gif", "image_gif" },
            { ".bmp", "image_bmp" }
        };

        public static string GetMIMEType(string fileName)
        {
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            return MimeTypes.ContainsKey(fileExtension) ? MimeTypes[fileExtension] : "binary_octet_stream";
        }
    }
}