using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId, CancellationToken cancellationToken = default);

    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, string description, string? extendedType = null, CancellationToken cancellationToken = default);
}
