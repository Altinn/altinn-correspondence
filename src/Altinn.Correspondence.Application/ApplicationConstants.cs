namespace Altinn.Correspondence.Application.Settings;

public static class ApplicationConstants
{
    public const long MaxFileUploadSize = 250 * 1024 * 1024;
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
    public static readonly List<string> RequiredOrganizationRolesForCorrespondenceRecipient = 
    [
        "BEST",
        "DAGL",
        "DTPR",
        "DTSO",
        "INNH",
        "LEDE",
        "REPR"
    ];
}