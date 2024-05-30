using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceStatusRepository
    {
        Task<Guid> AddCorrespondenceStatus(CorrespondenceStatusEntity Correspondence, CancellationToken cancellationToken);
        Task<CorrespondenceStatusEntity?> GetLatestStatusByCorrespondenceId(Guid CorrespondenceId, CancellationToken cancellationToken);
    }
}