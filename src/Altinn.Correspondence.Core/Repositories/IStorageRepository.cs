using System.Net.Mail;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IStorageRepository
    {
        Task<string?> UploadAttachment(Guid attachmentId, Stream attachment, CancellationToken cancellationToken);
        Task<Stream> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken);
        Task DeleteAttachment(Guid attachmentId, CancellationToken cancellationToken);
    }
}
