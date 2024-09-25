namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentRequest
{
    public Guid CorrespondenceId { get; set; }
    public Guid AttachmentId { get; set; }
}
