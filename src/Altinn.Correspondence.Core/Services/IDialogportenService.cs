using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialogForMigratedCorrespondence(Guid correspondenceId, CorrespondenceEntity? correspondence, bool enableEvents = false, bool isSoftDeleted = false);
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId);
    Task<string> CreateDialogTransmission(Guid correspondenceId);
    Task PatchCorrespondenceDialogToConfirmed(Guid correspondenceId);
    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, DateTimeOffset activityTimestamp, params string[] tokens);
    Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp);
    Task CreateConfirmedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp);
    Task PurgeCorrespondenceDialog(Guid correspondenceId);
    Task SoftDeleteDialog(string dialogId);
    Task<bool> TrySoftDeleteDialog(string dialogId);
    Task<bool> TryRemoveDialogExpiresAt(string dialogId, CancellationToken cancellationToken = default);
    Task<bool> TryRestoreSoftDeletedDialog(string dialogId, CancellationToken cancellationToken = default);
    Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp);
    Task UpdateSystemLabelsOnDialog(Guid correspondenceId, string enduserId, List<DialogPortenSystemLabel>? systemLabelsToAdd, List<DialogPortenSystemLabel>? systemLabelsToRemove);
}
