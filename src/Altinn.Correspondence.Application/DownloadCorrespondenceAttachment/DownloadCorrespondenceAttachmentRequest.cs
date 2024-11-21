namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required Guid AttachmentId { get; set; }
    public string? OnBehalfOf { get; set; }
}
