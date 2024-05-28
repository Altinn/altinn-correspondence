using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceRepository
    {
        Task<Guid> InitializeCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken);
        Task<(List<Guid>, int)> GetCorrespondences(int offset,
            int limit,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CorrespondenceStatus? status,
            CancellationToken cancellationToken);
    }
}