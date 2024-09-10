using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId, CancellationToken cancellationToken = default);

    Task CreateCorrespondenceStatusUpdateDialogActivity(Guid correspondenceId, CorrespondenceStatus status, CancellationToken cancellationToken = default);
}
