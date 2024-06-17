namespace Altinn.Correspondence.Application.AddCorrespondenceAttachment;

public class AddCorrespondenceAttachmentRequest
{
    public Guid AttachmentId { get; set; }
    public Guid CorrespondenceId { get; set; }
}
