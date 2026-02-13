using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Helpers;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Altinn.Platform.Register.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten;

public class DialogportenService(HttpClient _httpClient, ICorrespondenceRepository _correspondenceRepository, IAltinnRegisterService altinnRegisterService, IOptions<GeneralSettings> generalSettings, ILogger<DialogportenService> logger, IIdempotencyKeyRepository _idempotencyKeyRepository, IResourceRegistryService _resourceRegistryService, ICorrespondenceForwardingEventRepository correspondenceForwardingEventRepository) : IDialogportenService
{
    public async Task<string> CreateCorrespondenceDialog(Guid correspondenceId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        logger.LogDebug("CreateCorrespondenceDialog for correspondence {correspondenceId}", correspondence.Id);

        // Create idempotency key for open dialog activity
        await CreateIdempotencyKeysForCorrespondence(correspondence, cancellationToken);

        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, generalSettings.Value.CorrespondenceBaseUrl, false, logger);
        var response = await _httpClient.PostAsJsonAsync("dialogporten/api/v1/serviceowner/dialogs", createDialogRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            logger.LogError(errorMessage);
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var dialogResponse = await response.Content.ReadFromJsonAsync<string>(cancellationToken);
        if (dialogResponse is null)
        {
            throw new Exception("Dialogporten did not return a dialogId");
        }
        return dialogResponse;
    }

    public async Task<string> CreateDialogTransmission(Guid correspondenceId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        logger.LogDebug("CreateDialogTransmission for correspondence {correspondenceId}", correspondence.Id);

        // Create idempotency key for open dialog activity
        await CreateIdempotencyKeysForCorrespondence(correspondence, cancellationToken);

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;

        var createTransmissionRequest = CreateDialogTransmissionMapper.CreateDialogTransmission(correspondence, generalSettings.Value.CorrespondenceBaseUrl, false, logger);
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/transmissions", createTransmissionRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var transmissionResponse = await response.Content.ReadFromJsonAsync<string>(cancellationToken);
        if (transmissionResponse is null)
        {
            throw new Exception("Dialogporten did not return a transmissionId");
        }
        return transmissionResponse;
    }

    public async Task<bool> PatchCorrespondenceDialogToConfirmed(Guid correspondenceId, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping patching correspondence {correspondenceId} to confirmed as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return false;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }
        var patchRequestBuilder = new DialogPatchRequestBuilder();
        var dialog = await GetDialog(dialogId);
        string confirmEndpointUrl = $"{generalSettings.Value.CorrespondenceBaseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm";
        var guiActionIndexToDelete = dialog.GuiActions?.FindIndex(dialog => dialog.Url == confirmEndpointUrl) ?? -1;
        var apiActionIndexToDelete = dialog.ApiActions?.FindIndex(dialog => dialog.Endpoints.Any(endpoint => endpoint.Url == confirmEndpointUrl)) ?? -1;
        if (guiActionIndexToDelete != -1)
        {
            patchRequestBuilder.WithRemoveGuiActionOperation(guiActionIndexToDelete);
        }
        if (apiActionIndexToDelete != -1)
        {
            patchRequestBuilder.WithRemoveApiActionOperation(apiActionIndexToDelete);
        }
        if (dialog.Status == "RequiresAttention")
        {
            patchRequestBuilder.WithReplaceStatusOperation("NotApplicable");
        }
        var patchRequest = patchRequestBuilder.Build();
        if (patchRequest.Count == 0)
        {
            logger.LogDebug("No actions to remove from dialog {dialogId} for correspondence {correspondenceId}", dialogId, correspondenceId);
            return false;
        }
        var response = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}?isSilentUpdate=true", patchRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Response from Dialogporten when patching dialog {dialogId} to confirmed for correspondence {correspondenceId} was not successful: {statusCode}: {responseContent}",
                dialogId,
                correspondenceId,
                response.StatusCode,
                responseContent);
            throw new Exception($"Dialogporten patch to confirmed failed: {response.StatusCode}: {responseContent}");
        }
        return true;
    }

    public async Task<bool> VerifyCorrespondenceDialogPatchedToConfirmed(Guid correspondenceId, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                // Nothing to verify for migrated correspondences without a dialog.
                return true;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var dialog = await GetDialog(dialogId);
        string confirmEndpointUrl = $"{generalSettings.Value.CorrespondenceBaseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm";

        var hasGuiAction = dialog.GuiActions?.Any(a => a.Url == confirmEndpointUrl) ?? false;
        var hasApiAction = dialog.ApiActions?.Any(a => a.Endpoints.Any(e => e.Url == confirmEndpointUrl)) ?? false;
        var statusOk = !string.Equals(dialog.Status, "RequiresAttention", StringComparison.OrdinalIgnoreCase);

        return !hasGuiAction && !hasApiAction && statusOk;
    }
    public async Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, string? partyUrn, DateTimeOffset activityTimestamp, params string[] tokens)
    {
        logger.LogDebug("CreateInformationActivity {actorType}: {textType} for correspondence {correspondenceId}",
            Enum.GetName(typeof(DialogportenActorType), actorType),
            Enum.GetName(typeof(DialogportenTextType), textType),
            correspondenceId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping creating information activity for {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, textType, ActivityType.Information, partyUrn, activityTimestamp, tokens);

        // Only set activity ID for download events using the stored idempotency key
        if (textType == DialogportenTextType.DownloadStarted)
        {
            if (tokens.Length < 2 || !Guid.TryParse(tokens[1], out var attachmentId))
            {
                logger.LogError("Invalid attachment ID token for download activity on correspondence {correspondenceId}", correspondenceId);
                throw new ArgumentException("Invalid attachment ID token", nameof(tokens));
            }

            var existingIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
                correspondence.Id,
                attachmentId,
                partyUrn?.WithUrnPrefix(),
                StatusAction.AttachmentDownloaded,
                IdempotencyType.DialogportenActivity,
                cancellationToken);

            if (existingIdempotencyKey is null)
            {
                existingIdempotencyKey = await _idempotencyKeyRepository.CreateAsync(
                    new IdempotencyKeyEntity
                    {
                        Id = Uuid.NewDatabaseFriendly(Database.PostgreSql),
                        CorrespondenceId = correspondence.Id,
                        AttachmentId = attachmentId,
                        PartyUrn = partyUrn?.WithUrnPrefix(),
                        StatusAction = StatusAction.AttachmentDownloaded,
                        IdempotencyType = IdempotencyType.DialogportenActivity
                    },
                    cancellationToken);
            }
            createDialogActivityRequest.Id = existingIdempotencyKey.Id.ToString();
        }

        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities?isSilentUpdate=true", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("already exists"))
                {
                    logger.LogWarning("Activity already exists for correspondence {correspondenceId} and dialog {dialogId}", correspondenceId, dialogId);
                    return; // Skip if the activity already exists
                }
            }
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, DateTimeOffset activityTimestamp, params string[] tokens)
    {
        await CreateInformationActivity(correspondenceId, actorType, textType, null, activityTimestamp, tokens);
    }

    public async Task<bool> HasInformationActivityByTextType(Guid correspondenceId, DialogportenTextType textType, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                return true;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var dialog = await GetDialog(dialogId);
        if (dialog.Activities is null || dialog.Activities.Count == 0)
        {
            return false;
        }

        foreach (var activity in dialog.Activities)
        {
            if (!string.Equals(activity.Type, "Information", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (activity.Description is null || activity.Description.Count == 0)
            {
                continue;
            }

            foreach (var d in activity.Description)
            {
                if (string.Equals(d.LanguageCode, "nb", StringComparison.OrdinalIgnoreCase) &&
                    DialogportenText.IsTemplate(textType, DialogportenLanguageCode.NB, d.Value))
                {
                    return true;
                }
                if (string.Equals(d.LanguageCode, "nn", StringComparison.OrdinalIgnoreCase) &&
                    DialogportenText.IsTemplate(textType, DialogportenLanguageCode.NN, d.Value))
                {
                    return true;
                }
                if (string.Equals(d.LanguageCode, "en", StringComparison.OrdinalIgnoreCase) &&
                    DialogportenText.IsTemplate(textType, DialogportenLanguageCode.EN, d.Value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public async Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp, string? partyUrn)
    {
        logger.LogDebug("CreateOpenedActivity by {actorType} for correspondence {correspondenceId}",
            Enum.GetName(typeof(DialogportenActorType), actorType),
            correspondenceId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping creating opened activity for {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        // Get the pre-created idempotency key for open dialog activity
        var existingOpenIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
            correspondenceId,
            null, // No attachment for opened activity
            null,
            StatusAction.Fetched,
            IdempotencyType.DialogportenActivity,
            cancellationToken);

        if (existingOpenIdempotencyKey == null)
        {
            existingOpenIdempotencyKey = await _idempotencyKeyRepository.CreateAsync(
                new IdempotencyKeyEntity
                {
                    Id = Uuid.NewDatabaseFriendly(Database.PostgreSql),
                    CorrespondenceId = correspondence.Id,
                    AttachmentId = null, // No attachment for opened activity
                    PartyUrn = null, // One open per correspondence
                    StatusAction = StatusAction.Fetched,
                    IdempotencyType = IdempotencyType.DialogportenActivity
                },
                cancellationToken);
        }

        var createDialogActivityRequest = CreateOpenedActivityRequest(correspondence, actorType, activityTimestamp, partyUrn);
        createDialogActivityRequest.Id = existingOpenIdempotencyKey.Id.ToString(); // Use the created activity ID
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities?isSilentUpdate=true", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("already exists"))
                {
                    logger.LogWarning("Activity already exists for correspondence {correspondenceId} and dialog {dialogId}", correspondenceId, dialogId);
                    return; // Skip if the activity already exists
                }
            }
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

    }

    public async Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp)
    {
        await CreateOpenedActivity(correspondenceId, actorType, activityTimestamp, null);
    }

    public async Task CreateConfirmedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp, string? partyUrn)
    {
        logger.LogDebug("CreateConfirmedActivity by {actorType} for correspondence {correspondenceId}",
            Enum.GetName(typeof(DialogportenActorType), actorType),
            correspondenceId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        if (correspondence.Statuses.Count(s => s.Status == CorrespondenceStatus.Confirmed) >= 2)
        {
            logger.LogDebug("Correspondence with id {correspondenceId} already has a Confirmed status, skipping activity creation on Dialogporten", correspondenceId);
            return;
        }

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping creating confirmed activity for {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        // Get the pre-created idempotency key for confirm activity
        var existingConfirmIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
            correspondence.Id,
            null, // No attachment for confirm activity
            null, // Log once for each Correspondence, not per recipient
            StatusAction.Confirmed,
            IdempotencyType.DialogportenActivity,
            cancellationToken);

        if (existingConfirmIdempotencyKey == null)
        {
            existingConfirmIdempotencyKey = await _idempotencyKeyRepository.CreateAsync(
                new IdempotencyKeyEntity
                {
                    Id = Uuid.NewDatabaseFriendly(Database.PostgreSql),
                    CorrespondenceId = correspondence.Id,
                    AttachmentId = null, // No attachment for confirm activity
                    PartyUrn = null, // One confirmation per correspondence
                    StatusAction = StatusAction.Confirmed,
                    IdempotencyType = IdempotencyType.DialogportenActivity
                },
                cancellationToken);
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, ActivityType.CorrespondenceConfirmed, partyUrn, activityTimestamp);
        createDialogActivityRequest.Id = existingConfirmIdempotencyKey.Id.ToString(); // Use the created activity ID

        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities?isSilentUpdate=true", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("already exists"))
                {
                    logger.LogWarning("Activity already exists for correspondence {correspondenceId} and dialog {dialogId}", correspondenceId, dialogId);
                    return; // Skip if the activity already exists
                }
            }
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task CreateConfirmedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp)
    {
        await CreateConfirmedActivity(correspondenceId, actorType, activityTimestamp, null);
    }


    public async Task PurgeCorrespondenceDialog(Guid correspondenceId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping purging correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var response = await _httpClient.PostAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/actions/purge", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task SoftDeleteDialog(string dialogId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var response = await _httpClient.DeleteAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task<bool> TrySoftDeleteDialog(string dialogId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var response = await _httpClient.DeleteAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone)
        {
            return false;
        }
        throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    public async Task<bool> TryRemoveDialogExpiresAt(string dialogId, CancellationToken cancellationToken = default)
    {
        var dialog = await GetDialog(dialogId);
        if (dialog is null)
        {
            throw new Exception($"Dialog {dialogId} not found when attempting to remove expiresAt");
        }
        if (dialog.ExpiresAt == null)
        {
            return false;
        }
        var patchRequestBuilder = new DialogPatchRequestBuilder()
            .WithRemoveExpiresAtOperation();
        var patchRequest = patchRequestBuilder.Build();
        var response = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", patchRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(($"Response from Dialogporten when removing expiresAt for {dialogId} was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"));
            return false;
        }
        return true;
    }

    public async Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp, string? partyUrn)
    {
        logger.LogDebug("CreateCorrespondencePurgedActivity by {actorType}: for correspondence {correspondenceId}",
            Enum.GetName(typeof(DialogportenActorType), actorType),
            correspondenceId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, true, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping creating dialog purged activity for correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }
        var existingIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
            correspondence.Id,
            null, // No attachment for purged activity
            null,
            StatusAction.PurgedByRecipient,
            IdempotencyType.DialogportenActivity,
            cancellationToken);

        if (existingIdempotencyKey is null)
        {
            existingIdempotencyKey = new IdempotencyKeyEntity
            {
                Id = Uuid.NewDatabaseFriendly(Database.PostgreSql),
                CorrespondenceId = correspondence.Id,
                AttachmentId = null,
                StatusAction = StatusAction.PurgedByRecipient,
                IdempotencyType = IdempotencyType.DialogportenActivity
            };
            await _idempotencyKeyRepository.CreateAsync(existingIdempotencyKey, cancellationToken);
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, Models.ActivityType.DialogDeleted, partyUrn, activityTimestamp);
        createDialogActivityRequest.Id = existingIdempotencyKey.Id.ToString();
        if (actorType != DialogportenActorType.ServiceOwner)
        {
            createDialogActivityRequest.PerformedBy.ActorName = actorName;
            createDialogActivityRequest.PerformedBy.ActorId = null;
        }
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities?isSilentUpdate=true", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }


    public async Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp)
    {
        await CreateCorrespondencePurgedActivity(correspondenceId, actorType, actorName, activityTimestamp, null);
    }

    public async Task<CreateDialogRequest> GetDialog(string dialogId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var response = await _httpClient.GetAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DialogNotFoundException(dialogId);
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        var dialogRequest = await response.Content.ReadFromJsonAsync<CreateDialogRequest>(cancellationToken);
        if (dialogRequest is null)
        {
            throw new Exception("Failed to deserialize the dialog request from the response.");
        }
        return dialogRequest;
    }

    private async Task<(Guid OpenedId, Guid? ConfirmedId)> CreateIdempotencyKeysForCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        // Create idempotency key for open dialog activity
        var openActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        var openIdempotencyKey = new IdempotencyKeyEntity
        {
            Id = openActivityId,
            CorrespondenceId = correspondence.Id,
            AttachmentId = null,
            StatusAction = StatusAction.Fetched,
            IdempotencyType = IdempotencyType.DialogportenActivity
        };
        await _idempotencyKeyRepository.CreateAsync(openIdempotencyKey, cancellationToken);

        // Create idempotency key for confirm activity if confirmation is needed
        Guid? confirmActivityId = null;
        if (correspondence.IsConfirmationNeeded)
        {
            confirmActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
            var confirmIdempotencyKey = new IdempotencyKeyEntity
            {
                Id = confirmActivityId.Value,
                CorrespondenceId = correspondence.Id,
                AttachmentId = null,
                StatusAction = StatusAction.Confirmed,
                IdempotencyType = IdempotencyType.DialogportenActivity
            };
            await _idempotencyKeyRepository.CreateAsync(confirmIdempotencyKey, cancellationToken);
        }

        // Create idempotency keys for each attachment's download activity
        var attachmentIdempotencyKeys = new List<IdempotencyKeyEntity>();
        foreach (var attachment in correspondence.Content?.Attachments ?? Enumerable.Empty<CorrespondenceAttachmentEntity>())
        {
            var downloadActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
            var downloadIdempotencyKey = new IdempotencyKeyEntity
            {
                Id = downloadActivityId,
                CorrespondenceId = correspondence.Id,
                AttachmentId = attachment.AttachmentId,
                StatusAction = StatusAction.AttachmentDownloaded
            };
            attachmentIdempotencyKeys.Add(downloadIdempotencyKey);
        }
        await _idempotencyKeyRepository.CreateRangeAsync(attachmentIdempotencyKeys, cancellationToken);

        return (openActivityId, confirmActivityId);
    }

    public async Task<bool> TryRemoveMarkdownAndHtmlFromSummary(string dialogId, string newSummary, CancellationToken cancellationToken = default)
    {
        var dialog = await GetDialog(dialogId);
        if (dialog is null)
        {
            throw new Exception($"Dialog {dialogId} not found when attempting to remove markdown and html from summary");
        }

        var strippedSummary = newSummary;
        var patchRequestBuilder = new DialogPatchRequestBuilder()
            .WithReplaceSummaryOperation(strippedSummary);
        var patchRequest = patchRequestBuilder.Build();
        var response = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", patchRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(($"Response from Dialogporten when removing markdown and html from summary for {dialogId} was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"));
            return false;
        }
        return true;
    }

    public async Task<bool> ValidateDialogRecipientMatch(string dialogId, string expectedRecipient, CancellationToken cancellationToken = default)
    {

        CreateDialogRequest? dialog = await GetDialog(dialogId);
        return dialog.Party.WithoutPrefix() == expectedRecipient.WithoutPrefix();
    }

    public async Task<bool> DialogValidForTransmission(string dialogId, string transmissionResourceId, CancellationToken cancellationToken = default)
    {
        CreateDialogRequest? dialog = await GetDialog(dialogId);

        var dialogResource = dialog.ServiceResource.WithoutPrefix();
        var normalizedTransmissionResourceId = transmissionResourceId.WithoutPrefix();
        var dialogResourceOwner = await _resourceRegistryService.GetServiceOwnerNameOfResource(dialogResource, cancellationToken);
        var transmissionResourceOwner = await _resourceRegistryService.GetServiceOwnerNameOfResource(normalizedTransmissionResourceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(dialogResourceOwner) || string.IsNullOrWhiteSpace(transmissionResourceOwner))
        {
            return false;
        }
        return string.Equals(dialogResourceOwner, transmissionResourceOwner, StringComparison.OrdinalIgnoreCase);
    }

    public CreateDialogActivityRequest CreateOpenedActivityRequest(CorrespondenceEntity correspondence, DialogportenActorType actorType, DateTimeOffset activityTimestamp, string? partyUrn)
    {
        if (TransmissionValidator.IsTransmission(correspondence))
        {
            return CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, ActivityType.TransmissionOpened, partyUrn, activityTimestamp);
        }
        else
        {
            return CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, ActivityType.CorrespondenceOpened, partyUrn, activityTimestamp);
        }
    }

    public async Task<DialogPortenSystemLabel> GetDialogportenSystemLabel(List<ExternalReferenceEntity> externalReferences)
    {
        var dialogId = externalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (string.IsNullOrWhiteSpace(dialogId))
        {
            throw new ArgumentException("Missing or empty dialog ID in external references");
        }

        var dialog = await GetDialog(dialogId);
        if (Enum.TryParse(dialog.SystemLabel, ignoreCase: true, out DialogPortenSystemLabel label))
        {
            return label;
        }

        return DialogPortenSystemLabel.Default;
    }


    #region MigrationRelated    
    /// <summary>
    /// Create Dialog in Dialogportern without creating any events. Used in regards to old correspondences being migrated from Altinn 2 to Altinn 3.
    /// </summary>
    public async Task<string> CreateCorrespondenceDialogForMigratedCorrespondence(Guid correspondenceId, CorrespondenceEntity? correspondence, bool enableEvents = false, bool isSoftDeleted = false)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        if (correspondence is null)
        {
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        if (correspondence.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId))
        {
            logger.LogWarning($"Duplicate job for correspondence {correspondenceId}");
            return correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue ?? string.Empty;
        }

        var (OpenedId, ConfirmedId) = await CreateIdempotencyKeysForCorrespondence(correspondence, cancellationToken);

        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(
            correspondence: correspondence,
            baseUrl: generalSettings.Value.CorrespondenceBaseUrl,
            includeActivities: true,
            logger: logger,
            openedActivityIdempotencyKey: OpenedId.ToString(),
            confirmedActivityIdempotencyKey: ConfirmedId?.ToString(),
            isSoftDeleted: isSoftDeleted);
        string updateType = enableEvents ? "" : "?IsSilentUpdate=true";
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs{updateType}", createDialogRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                logger.LogError($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                return string.Empty;
            }
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var dialogResponse = await response.Content.ReadFromJsonAsync<string>(cancellationToken);
        if (dialogResponse is null)
        {
            throw new Exception("Dialogporten did not return a dialogId");
        }
        return dialogResponse;
    }

    /// <summary>
    /// Method to add or remove system labels on a dialog in Dialogporten.
    /// Used for setting the "Archived" system label when a correspondence is archived in Altinn 2, or adding/removing "Bin" labels when a correspondence is soft deleted/restored in Altinn 2.
    /// </summary>
    /// <param name="correspondenceId">ID of the correspondence</param>
    /// <param name="performedByActorId">Actor id of the user who performed the action</param>
    /// <param name="performedByActorType">Type of actor who performed the action</param>
    /// <param name="systemLabelsToAdd">list of labels to add</param>
    /// <param name="systemLabelsToRemove">list of labels to remove</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task UpdateSystemLabelsOnDialog(Guid correspondenceId, string performedByActorId, DialogportenActorType performedByActorType, List<DialogPortenSystemLabel>? systemLabelsToAdd, List<DialogPortenSystemLabel>? systemLabelsToRemove)
    {
        if (string.IsNullOrWhiteSpace(performedByActorId))
        {
            logger.LogError("Missing performedByActorId for correspondence {correspondenceId} when updating system labels", correspondenceId);
            throw new ArgumentException("performedByActorId cannot be null or whitespace", nameof(performedByActorId));
        }

        if ((systemLabelsToAdd == null || systemLabelsToAdd.Count == 0) && (systemLabelsToRemove == null || systemLabelsToRemove.Count == 0))
        {
            throw new ArgumentException("Either systemLabelsToAdd or systemLabelsToRemove must be provided");
        }
        if (systemLabelsToAdd != null && systemLabelsToRemove != null)
        {
            var overlap = systemLabelsToAdd
                .Cast<DialogPortenSystemLabel>()
                .Intersect(systemLabelsToRemove.Cast<DialogPortenSystemLabel>())
                .ToList();
            if (overlap.Count > 0)
            {
                throw new ArgumentException(
                    $"Label(s) present in both add and remove: {string.Join(", ", overlap)}");
            }
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping setting archived system label for correspondence {correspondenceId} as it is an Altinn2 correspondence not yet available.", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }
        if (!Guid.TryParse(dialogId, out var dialogGuid))
        {
            logger.LogError("DialogId {dialogId} is not a valid GUID for correspondence {correspondenceId}", dialogId, correspondenceId);
            throw new ArgumentException($"DialogId {dialogId} is not a valid GUID for correspondence {correspondenceId}");
        }
        var request = SetDialogSystemLabelsMapper
            .CreateSetDialogSystemLabelRequest(
                dialogGuid,
                performedByActorId,
                performedByActorType,
                systemLabelsToAdd,
                systemLabelsToRemove);
        logger.LogDebug("Updating system labels on dialog {dialogId} for correspondence {correspondenceId} for {performedByActorId}, type {performedByActorType}. Adding: {systemLabelsToAdd}, Removing: {systemLabelsToRemove}",
            dialogId,
            correspondenceId,
            performedByActorId,
            performedByActorType,
            systemLabelsToAdd != null ? string.Join(", ", systemLabelsToAdd) : "None",
            systemLabelsToRemove != null ? string.Join(", ", systemLabelsToRemove) : "None"
        );
        var url = $"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/endusercontext/systemlabels";
        var response = await _httpClient.PutAsJsonAsync(url, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Gone)
            {
                logger.LogWarning("Dialog {dialogId} for correspondence {correspondenceId} is already deleted in Dialogporten when attempting to update system labels. Response: {responseStatusCode}: {responseContent}",
                    dialogId,
                    correspondenceId,
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync());
                return;
            }
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()} when setting system labels for dialogid {dialogId} for correpondence {correspondenceId}");
        }
    }
    #endregion

    public async Task<bool> TryRestoreSoftDeletedDialog(string dialogId, CancellationToken cancellationToken = default)
    {
        // We assume Dialogporten exposes a restore endpoint for soft-deleted dialogs
        var response = await _httpClient.PostAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/actions/restore", null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone)
        {
            // Treat as already restored or not applicable
            return false;
        }
        throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    public async Task AddForwardingEvent(Guid forwardingEventId, CancellationToken cancellationToken)
    {
        var forwardingEvent = await correspondenceForwardingEventRepository.GetForwardingEvent(forwardingEventId, cancellationToken);

        var forwardedByParty = await altinnRegisterService
            .LookUpPartyByPartyUuid(forwardingEvent.ForwardedByPartyUuid, cancellationToken);
        if (forwardedByParty == null)
        {
            throw new Exception($"Could not find party for ForwardedByPartyUuid {forwardingEvent.ForwardedByPartyUuid} in forwarding event {forwardingEvent.Id}");
        }

        string forwardedByUrn;
        if (forwardedByParty.PartyTypeName == PartyType.Person)
        {
            forwardedByUrn = UrnConstants.PersonIdAttribute + ":" + forwardedByParty.SSN;
        }
        else if (forwardedByParty.PartyTypeName == PartyType.SelfIdentified)
        {
            forwardedByUrn = UrnConstants.PartyUuid + ":" + forwardedByParty.PartyUuid;
        }
        else
        {
            throw new Exception($"Unsupported party type {forwardedByParty.PartyTypeName} for ForwardedByPartyUuid {forwardingEvent.ForwardedByPartyUuid} in forwarding event {forwardingEvent.Id}");
        }

        if (forwardingEvent.ForwardedToUserUuid is not null) {
            // Instance delegation
            var forwardedToParty = await altinnRegisterService.LookUpPartyByPartyUuid(forwardingEvent.ForwardedToUserUuid.Value, cancellationToken);
            if (forwardedToParty == null)
            {
                throw new Exception($"Could not find party for ForwardedToUserUuid {forwardingEvent.ForwardedToUserUuid} in forwarding event {forwardingEvent.Id}");
            }
            string[] tokens =
            {
                forwardingEvent.Correspondence?.Content?.MessageTitle ?? string.Empty,
                forwardedToParty.Name,
                forwardingEvent.ForwardingText ?? string.Empty
            };

            await CreateInformationActivity(
                forwardingEvent.CorrespondenceId,
                DialogportenActorType.Recipient,
                DialogportenTextType.CorrespondenceInstanceDelegated,
                forwardedByUrn,
                forwardingEvent.ForwardedOnDate,
                tokens);
        }
        else if (!string.IsNullOrEmpty(forwardingEvent.ForwardedToEmailAddress))
        {
            // Email forwarding
            string[] tokens =
            {
                forwardingEvent.Correspondence?.Content?.MessageTitle ?? string.Empty,
                forwardingEvent.ForwardedToEmailAddress,
                forwardingEvent.ForwardingText ?? string.Empty
            };

            await CreateInformationActivity(
                forwardingEvent.CorrespondenceId,
                DialogportenActorType.Recipient,
                DialogportenTextType.CorrespondenceForwardedToEmail,
                forwardedByUrn,
                forwardingEvent.ForwardedOnDate,
                tokens);
        }
        else if (!string.IsNullOrWhiteSpace(forwardingEvent.MailboxSupplier))
        {
            // Mailbox forwarding
            var mailboxSupplierName = forwardingEvent.MailboxSupplier.ToLower() switch
            {
                "urn:altinn:organization:identifier-no:984661185" => "Digipost",
                "urn:altinn:organization:identifier-no:922020175" => "e-Boks",
                "urn:altinn:organization:identifier-no:996460320" => "e-Boks",
                _ => throw new Exception($"Unknown mailbox supplier {forwardingEvent.MailboxSupplier} in forwarding event {forwardingEvent.Id}")
            };

            string[] tokens =
            {
                forwardingEvent.Correspondence?.Content?.MessageTitle ?? string.Empty,
                mailboxSupplierName,
                forwardingEvent.ForwardingText ?? string.Empty
            };

            await CreateInformationActivity(
                forwardingEvent.CorrespondenceId,
                DialogportenActorType.Recipient,
                DialogportenTextType.CorrespondenceForwardedToMailboxSupplier, 
                forwardedByUrn,
                forwardingEvent.ForwardedOnDate,
                tokens);
        }
        else
        {
            throw new Exception($"Forwarding event {forwardingEvent.Id} has no valid forwarding target (no ForwardedToUserUuid, ForwardedToEmailAddress or MailboxSupplier)");
        }
    }
}
