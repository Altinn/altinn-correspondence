using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialogForMigratedCorrespondence(Guid correspondenceId, CorrespondenceEntity? correspondence, bool enableEvents = false);
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId);
    Task PatchCorrespondenceDialogToConfirmed(Guid correspondenceId);
    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, DateTimeOffset activityTimestamp, params string[] tokens);
    Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp);
    Task PurgeCorrespondenceDialog(Guid correspondenceId);
    Task SoftDeleteDialog(string dialogId);
    Task<bool> TrySoftDeleteDialog(string dialogId);
    Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp);
    Task SetArchivedSystemLabelOnDialog(Guid correspondenceId, string enduserId);
}
