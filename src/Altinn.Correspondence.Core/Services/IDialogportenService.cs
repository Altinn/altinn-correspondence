using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IDialogportenService
{
    Task<string> CreateCorrespondenceDialogForMigratedCorrespondence(Guid correspondenceId, CorrespondenceEntity? correspondence, bool enableEvents = false, bool isSoftDeleted = false);
    Task<string> CreateCorrespondenceDialog(Guid correspondenceId);
    Task<string> CreateDialogTransmission(Guid correspondenceId);
    Task<bool> PatchCorrespondenceDialogToConfirmed(Guid correspondenceId);
    Task<bool> VerifyCorrespondenceDialogPatchedToConfirmed(Guid correspondenceId, CancellationToken cancellationToken = default);
    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, DateTimeOffset activityTimestamp, params string[] tokens);
    Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, string partyUrn, DateTimeOffset activityTimestamp, params string[] tokens);
    Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp);
    Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp, string? partyUrn);
    Task CreateConfirmedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp);
    Task CreateConfirmedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp, string? partyUrn);
    Task PurgeCorrespondenceDialog(Guid correspondenceId);
    Task SoftDeleteDialog(string dialogId);
    Task<bool> TrySoftDeleteDialog(string dialogId);
    Task<bool> TryRemoveDialogExpiresAt(string dialogId, CancellationToken cancellationToken = default);
    Task<bool> TryRestoreSoftDeletedDialog(string dialogId, CancellationToken cancellationToken = default);
    Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp);
    Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp, string? partyUrn);
    Task UpdateSystemLabelsOnDialog(Guid correspondenceId, string enduserId, List<DialogPortenSystemLabel>? systemLabelsToAdd, List<DialogPortenSystemLabel>? systemLabelsToRemove);
    Task<bool> TryRemoveMarkdownAndHtmlFromSummary(string dialogId, string newSummary, CancellationToken cancellationToken = default);
    Task<bool> ValidateDialogRecipientMatch(string dialogId, string expectedRecipient, CancellationToken cancellationToken = default);
    Task<bool> DialogValidForTransmission(string dialogId, string transmissionResourceId, CancellationToken cancellationToken = default);
    Task<DialogPortenSystemLabel> GetDialogportenSystemLabel(List<ExternalReferenceEntity> externalReferences);
}
