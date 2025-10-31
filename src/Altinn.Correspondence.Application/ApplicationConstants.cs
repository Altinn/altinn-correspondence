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
        "bestyrende-reder",
        "daglig-leder",
        "deltaker-delt-ansvar",
        "deltaker-fullt-ansvar",
        "innehaver",
        "styreleder",
        "komplementar",
        "bostyrer",
        "kontaktperson-ados",
        "norsk-representant",
    ];

    public static readonly List<string> RequiredOrganizationRolesForConfidentialCorrespondenceRecipient = 
    [
        "bestyrende-reder",
        "daglig-leder",
        "deltaker-delt-ansvar",
        "deltaker-fullt-ansvar",
        "innehaver",
        "styreleder",
        "komplementar"
    ];
}