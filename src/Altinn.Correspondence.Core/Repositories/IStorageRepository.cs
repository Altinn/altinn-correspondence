using System.Net.Mail;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IStorageRepository
    {
        Task UploadAttachment(Guid attachmentId, Stream attachment, CancellationToken cancellationToken);
        Task<Stream> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken);
        Task PurgeAttachment(Guid attachmentId, CancellationToken cancellationToken);
        string GetBlobUri(Guid attachmentId);
        Task<string?> GetBlobhash(Guid attachmentId, CancellationToken cancellationToken);
    }
}
