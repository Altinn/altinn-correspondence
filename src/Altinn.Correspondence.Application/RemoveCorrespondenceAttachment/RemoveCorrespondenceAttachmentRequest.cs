namespace Altinn.Correspondence.Application.RemoveCorrespondenceAttachment;

public class RemoveCorrespondenceAttachmentRequest
{
    public Guid AttachmentId { get; set; }
    public Guid CorrespondenceId { get; set; }
}
