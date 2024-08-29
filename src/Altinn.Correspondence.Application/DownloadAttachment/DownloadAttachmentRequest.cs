namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentRequest
{
    public Guid CorrespondenceId { get; set; }
    public Guid AttachmentId { get; set; }
}
