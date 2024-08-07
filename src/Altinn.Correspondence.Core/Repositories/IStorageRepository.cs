using System.Net.Mail;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IStorageRepository
    {
        Task<(string locationUrl, string hash)> UploadAttachment(AttachmentEntity attachment, Stream stream, CancellationToken cancellationToken);
        Task<Stream> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken);
        Task PurgeAttachment(Guid attachmentId, CancellationToken cancellationToken);
        string GetBlobUri(Guid attachmentId);
        Task<string?> GetBlobhash(Guid attachmentId, CancellationToken cancellationToken);
    }
}
