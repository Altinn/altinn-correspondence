using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IMigrationRepository
    {
        Task<CorrespondenceMigrationStatusEntity?> GetCorrespondenceMigrationStatus(Guid correspondenceId, CancellationToken cancellationToken);
    }
}