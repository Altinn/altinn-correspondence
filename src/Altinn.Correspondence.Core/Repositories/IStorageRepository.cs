using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IStorageRepository
    {
        Task<(string locationUrl, string hash, long size)> UploadAttachment(AttachmentEntity attachment, Stream stream, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken);
        Task<Stream> DownloadAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken);
        Task PurgeAttachment(Guid attachmentId, StorageProviderEntity? storageProviderEntity, CancellationToken cancellationToken);
        Task<(string locationUrl, string hash, long size)> UploadReportFile(string fileName, int serviceOwnerCount, int correspondencCount, Stream stream, CancellationToken cancellationToken);
        Task<Stream> DownloadReportFile(string fileName, CancellationToken cancellationToken);
        Task<(Stream DownloadStream, string FileName, long FileSize, string FileHash, int ServiceOwnerCount, int CorrespondenceCount)> DownloadLatestReportFile(CancellationToken cancellationToken);
    }
}
