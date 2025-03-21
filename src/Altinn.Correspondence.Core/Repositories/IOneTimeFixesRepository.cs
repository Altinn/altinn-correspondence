
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Repositories
{
    public interface IOneTimeFixesRepository
    {
        Task<List<CorrespondenceEntity>> GetCorrespondenceForNameFix(CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondencesWithoutConfirmation(CancellationToken cancellationToken);

        Task<List<CorrespondenceEntity>> GetCorrespondences(CancellationToken cancellationToken);
        Task<List<CorrespondenceEntity>> GetCorrespondencesWithoutOpenedStatus(CancellationToken cancellationToken);
        Task<List<CorrespondenceEntity>> GetCorrespondencesWithArchivedAction(CancellationToken cancellationToken);
    }
}