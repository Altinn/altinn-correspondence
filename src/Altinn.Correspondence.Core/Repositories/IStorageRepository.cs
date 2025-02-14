using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IStorageRepository
    {
        Task<(string locationUrl, string hash, long size)> UploadAttachment(AttachmentEntity attachment, Stream stream, CancellationToken cancellationToken);
        Task<Stream> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken);
        Task PurgeAttachment(Guid attachmentId, CancellationToken cancellationToken);
    }
}
