namespace Altinn.Correspondence.Application.DownloadAllCorrespondenceAttachments;

public class DownloadAllCorrespondenceAttachmentsResponse
{
    public required Stream Stream { get; set; }
    public required string zipFileName { get; set; }
}
