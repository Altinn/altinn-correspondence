namespace Altinn.Correspondence.Application.Settings;

public static class ApplicationConstants
{
    public const long MaxFileUploadSize = 2L * 1000 * 1000 * 1000;
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
        "KOMP",
        "BOBE"
    ];

    public static readonly List<string> RequiredOrganizationRolesForConfidentialCorrespondenceRecipient = 
    [
        "BEST",
        "DAGL",
        "DTPR",
        "DTSO",
        "INNH",
        "LEDE",
        "KOMP"
    ];
}