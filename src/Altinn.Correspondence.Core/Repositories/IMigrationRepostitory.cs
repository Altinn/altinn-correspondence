using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IMigrationRepository
    {
        Task<CorrespondenceMigrationStatusEntity?> GetCorrespondenceMigrationStatus(Guid correspondenceId, CancellationToken cancellationToken);
    }
}