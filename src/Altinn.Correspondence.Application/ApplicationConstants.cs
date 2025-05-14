namespace Altinn.Correspondence.Application.Settings;

public static class ApplicationConstants
{
    public const long MaxFileUploadSize = 2 * 1024 * 1024 * 1024L;
    public static readonly List<string> AllowedFileTypes =
    [
        ".doc",
        ".xls",
        ".docx",
        ".xlsx",
        ".ppt",
        ".pps",
        ".zip",
        ".pdf",
        ".html",
        ".txt",
        ".xml",
        ".jpg",
        ".gif",
        ".bmp",
        ".png",
        ".json"
    ];
}