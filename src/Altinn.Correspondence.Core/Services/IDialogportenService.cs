using Altinn.Correspondence.Core.Models.Dialogporten;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId);
    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, params string[] tokens);
    Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType);
    Task PurgeCorrespondenceDialog(Guid correspondenceId);
    Task SoftDeleteDialog(string dialogId);
    Task PatchData(string dialogId, PatchData data);
}
