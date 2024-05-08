using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface ICorrespondenceRepository
    {
        Task<Guid> InitializeCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken);
    }
}