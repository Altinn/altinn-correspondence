namespace Altinn.Correspondence.Application.UploadAttachment
{
    public class UploadAttachmentRequest
    {
        public Guid AttachmentId { get; set; }
        public required Stream UploadStream { get; set; }
        public long ContentLength { get; set; }
    }
}
