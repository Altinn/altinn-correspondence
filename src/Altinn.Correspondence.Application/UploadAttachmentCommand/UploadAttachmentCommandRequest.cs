namespace Altinn.Correspondence.Application.UploadAttachmentCommand
{
    public class UploadAttachmentCommandRequest
    {
        public Guid AttachmentId { get; set; }
        public required Stream UploadStream { get; set; }
        public long ContentLength { get; set; }
    }
}
