using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceStatusRepository
    {
        Task<Guid> AddCorrespondenceStatus(CorrespondenceStatusEntity correspondenceStatusEntity, CancellationToken cancellationToken);
        Task<List<CorrespondenceStatusEntity>> AddCorrespondenceStatuses(List<CorrespondenceStatusEntity> correspondenceStatusEntities, CancellationToken cancellationToken);
    }
}