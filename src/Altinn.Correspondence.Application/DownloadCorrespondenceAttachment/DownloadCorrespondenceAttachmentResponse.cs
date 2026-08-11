namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment
{
    public class DownloadCorrespondenceAttachmentResponse
    {
        public Stream Stream { get; set; } = Stream.Null;
        public string FileName { get; set; } = string.Empty;
    }
}
