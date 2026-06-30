using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Register.Contracts;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Helpers;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using UUIDNext;
using Hangfire;

namespace Altinn.Correspondence.Integrations.Dialogporten;

public class DialogportenService(HttpClient _httpClient,
                                 ICorrespondenceRepository _correspondenceRepository,
                                 ICorrespondenceForwardingEventRepository correspondenceForwardingEventRepository,
                                 ICorrespondenceNotificationRepository correspondenceNotificationRepository,
                                 IAltinnRegisterService altinnRegisterService,
                                 IOptions<GeneralSettings> generalSettings,
                                 ILogger<DialogportenService> logger,
                                 IIdempotencyKeyRepository _idempotencyKeyRepository,
                                 IResourceRegistryService _resourceRegistryService,
                                 PartyUrnHelper partyUrnHelper) : IDialogportenService
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
        var dialogParty = await GetDialogParty(correspondence);
        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, generalSettings.Value.CorrespondenceBaseUrl, false, logger, dialogParty: dialogParty, enableDownloadAll: generalSettings.Value.EnableDownloadAll);
        var response = await _httpClient.PostAsJsonAsync("dialogporten/api/v1/serviceowner/dialogs", createDialogRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("already exists"))
                {
                    logger.LogWarning("Dialog already exists for correspondence {correspondenceId}", correspondenceId);
                    var existingDialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
                    if (existingDialogId is null)
                    {
                        return createDialogRequest.Id; // Return the dialog ID from the request if it's not yet stored on the correspondence, it will be stored when the dialog is created on Dialogporten
                    }
                    return existingDialogId;
                }
            }
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

    public async Task CreateDownloadStartedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp, string? partyUrn, params string[] tokens)
    {
        partyUrn = await GetDialogActivityParty(partyUrn);
        if (tokens.Length < 2 || !Guid.TryParse(tokens[1], out var attachmentId))
        {
            logger.LogError("Invalid attachment ID token for download activity on correspondence {correspondenceId}", correspondenceId);
            throw new ArgumentException("Invalid attachment ID token", nameof(tokens));
        }
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var existingIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
            correspondenceId,
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
                    CorrespondenceId = correspondenceId,
                    AttachmentId = attachmentId,
                    PartyUrn = partyUrn?.WithUrnPrefix(),
                    StatusAction = StatusAction.AttachmentDownloaded,
                    IdempotencyType = IdempotencyType.DialogportenActivity
                },
                cancellationToken);
        }

        await CreateInformationActivity(correspondenceId, actorType, DialogportenTextType.DownloadStarted, partyUrn, existingIdempotencyKey.Id, activityTimestamp, tokens);
    }

    public async Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, string? partyUrn, Guid? dialogActivityId, DateTimeOffset activityTimestamp,params string[] tokens)
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
        partyUrn = await GetDialogActivityParty(partyUrn);
        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, textType, ActivityType.Information, partyUrn, activityTimestamp, tokens);

        if (dialogActivityId is not null)
        {
            createDialogActivityRequest.Id = dialogActivityId.ToString();
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


    // TODO - should be removed when old Hangfire invocations are gone
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
        await CreateInformationActivity(correspondenceId, actorType, textType, null, null, activityTimestamp, tokens);
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

        CreateDialogRequest? dialog = null;
        try {
            dialog = await GetDialog(dialogId);
        } catch (DialogNotFoundException) {
            return true; // Dialog not found, skipping.
        }
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
        var statusCode = response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            if (statusCode != HttpStatusCode.NotFound)
            {
                throw new Exception($"Response from Dialogporten was not successful: {statusCode}: {await response.Content.ReadAsStringAsync()}");
            }
            logger.LogWarning("Dialog {dialogId} was already purged in Dialogporten; proceeding with local reference removal for correspondence {correspondenceId}", dialogId, correspondenceId);
        }
        var externalReferencesRemoved = await _correspondenceRepository.RemoveExternalReference(correspondence, ReferenceType.DialogportenDialogId, cancellationToken);
        if (!externalReferencesRemoved)
        {
            logger.LogWarning("Failed to remove Dialogporten dialog reference for correspondence {correspondenceId} after purging dialog {dialogId}", correspondenceId, dialogId);
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

    public async Task<string> CreateConfidentialReminderDialog(ConfidentialReminderDialogDto reminder)
    {
        var createDialogRequest = CreateDialogRequestMapper.CreateConfidentialReminderDialog(reminder, generalSettings.Value.CorrespondenceBaseUrl);
        var response = await _httpClient.PostAsJsonAsync("dialogporten/api/v1/serviceowner/dialogs", createDialogRequest);
        if (!response.IsSuccessStatusCode){
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        var dialogResponse = await response.Content.ReadFromJsonAsync<string>();
        if (dialogResponse is null)
        {
            throw new Exception("Dialogporten did not return a dialogId");
        }
        return dialogResponse;
    }

    [AutomaticRetry(Attempts = 10)]
    public async Task TryAddDownloadAllAttachmentsToDialog(Guid correspondenceId, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        if (correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.PurgedByAltinn) || correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.PurgedByRecipient))
        {
            logger.LogError("Correspondence with id {correspondenceId} has been purged", correspondenceId);
            return;
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            logger.LogError("No dialog found on correspondence with id {correspondenceId} when attempting to add download all attachments", correspondenceId);
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId} when attempting to add download all attachments");
        }
        var dialog = await GetDialog(dialogId);
        if (dialog is null)
        {
            throw new Exception($"Dialog {dialogId} not found when attempting to add download all attachments");
        }

        if (dialog.Attachments?.Any(a => a.Urls != null && a.Urls.Any(u => u.Url.Contains("downloadall"))) == true)
        {
            logger.LogInformation("Dialog {dialogId} already has download all attachments, skipping adding it again", dialogId);
            return;
        }


        List<Attachment> attachments = dialog.Attachments ?? new List<Attachment>();
        bool hasAttachments = attachments.Count > 0;
        if (!hasAttachments){
            attachments = CreateDialogRequestMapper.GetAttachmentsForDialogPatchRequest(correspondence, generalSettings.Value.CorrespondenceBaseUrl);
        } else{
            logger.LogInformation("Trying to remove attachments from correspondence: {correspondenceId}", correspondence.Id);
            var patchRequestBuilderRemoveAttachments = new DialogPatchRequestBuilder()
                .WithRemoveAttachmentsOperation();
            var patchRequestRemoveAttachments = patchRequestBuilderRemoveAttachments.Build();
            var responseRemoveAttachments = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}?isSilentUpdate=true", patchRequestRemoveAttachments, cancellationToken);
            if (!responseRemoveAttachments.IsSuccessStatusCode){
                logger.LogError($"Response from Dialogporten when removing attachments for {dialogId} was not successful: {responseRemoveAttachments.StatusCode}: {await responseRemoveAttachments.Content.ReadAsStringAsync()}");
                throw new Exception($"Response from Dialogporten when removing attachments was not successful: {responseRemoveAttachments.StatusCode}: {await responseRemoveAttachments.Content.ReadAsStringAsync()}");
            }
        }

        logger.LogInformation("Trying to add download all attachments to correspondence: {correspondenceId}", correspondence.Id);
        var patchRequestBuilder = new DialogPatchRequestBuilder()
            .WithAddDownloadAllAttachmentsOperation(baseUrl: generalSettings.Value.CorrespondenceBaseUrl, correspondence: correspondence, attachments: attachments);
        var patchRequest = patchRequestBuilder.Build();
        var response = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}?isSilentUpdate=true", patchRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError($"Response from Dialogporten when adding download all attachments for {dialogId} was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            throw new Exception($"Response from Dialogporten when adding download all attachments was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    #region MigrationRelated
   
    private async Task<Dictionary<Guid, string>> GetDialogPortenActorIdsForStatusEvents(List<CorrespondenceStatusEntity> statusEvents, CancellationToken cancellationToken)
    {
        return await partyUrnHelper.GetDialogPortenActorIdsForStatusEvents(statusEvents, cancellationToken);
    }

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
        var dialogParty = await GetDialogParty(correspondence);

        var forwardingActivities = await BuildForwardingActivities(correspondence, cancellationToken);

        // Get party URNs for status events (Read and Confirmed) to use correct actor IDs
        var partyUrnsByPartyUuid = await GetDialogPortenActorIdsForStatusEvents(correspondence.Statuses, cancellationToken);

        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(
            correspondence: correspondence,
            baseUrl: generalSettings.Value.CorrespondenceBaseUrl,
            includeActivities: true,
            logger: logger,
            openedActivityIdempotencyKey: OpenedId.ToString(),
            confirmedActivityIdempotencyKey: ConfirmedId?.ToString(),
            isSoftDeleted: isSoftDeleted,
            dialogParty: dialogParty,
            forwardingActivities: forwardingActivities,
            enableDownloadAll: generalSettings.Value.EnableDownloadAll,
            partyUrnsByPartyUuid: partyUrnsByPartyUuid);
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
        if (response.StatusCode == HttpStatusCode.Gone)
        {
            logger.LogWarning("Dialog {dialogId} for correspondence {correspondenceId} is already deleted in Dialogporten when attempting to update system labels. Response: {responseStatusCode}: {responseContent}",
                dialogId,
                correspondenceId,
                response.StatusCode,
                await response.Content.ReadAsStringAsync());
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
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
        
        var correspondence = forwardingEvent.Correspondence!;
                
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning("Skipping adding forwarding event for correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondence.Id);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondence.Id}");
        }

        // Build the activity using shared logic
        var activity = await BuildForwardingActivity(forwardingEvent, correspondence, cancellationToken);

        // Convert the Activity to CreateDialogActivityRequest
        var createDialogActivityRequest = new CreateDialogActivityRequest
        {
            Id = activity.Id,
            CreatedAt = activity.CreatedAt,
            Type = Enum.Parse<ActivityType>(activity.Type),
            PerformedBy = new ActivityPerformedBy
            {
                ActorId = activity.PerformedBy.ActorId,
                ActorName = activity.PerformedBy.ActorName,
                ActorType = activity.PerformedBy.ActorType
            },
            Description = activity.Description.Select(d => new ActivityDescription
            {
                Value = d.Value,
                LanguageCode = d.LanguageCode
            }).ToList()
        };
        
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities?isSilentUpdate=true", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("already exists"))
                {
                    logger.LogWarning("Activity already exists for correspondence {correspondenceId} and dialog {dialogId}", correspondence.Id, dialogId);
                    return;
                }
            }
            logger.LogError($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); // Only log as we will run again for those that don't pass DP validation
        }
    }

    public async Task AddNotificationActivity(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await correspondenceNotificationRepository.GetNotificationById(notificationId, cancellationToken);
        if (notification == null)
        {
            logger.LogWarning("Notification with id {NotificationId} not found. Skipping.", notificationId);
            return;
        }

        var correspondence = notification.Correspondence!;

        var dialogId = correspondence.ExternalReferences
            .FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)
            ?.ReferenceValue;

        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning(
                    "Skipping adding notification activity for correspondence {CorrespondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", 
                    correspondence.Id);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondence.Id}");
        }

        // Build the activity using the existing mapper method
        var activity = CreateDialogRequestMapper.GetActivityFromAltinn2Notification(correspondence, notification);

        // Post activity directly without duplicate check (normal operation)
        await PostActivityToDialog(dialogId, notificationId, correspondence.Id, activity, cancellationToken);
    }

    /// <summary>
    /// Posts an activity to a Dialogporten dialog.
    /// </summary>
    private async Task PostActivityToDialog(string dialogId, Guid notificationId, Guid correspondenceId, Activity activity, CancellationToken cancellationToken)
    {
        var createDialogActivityRequest = new CreateDialogActivityRequest
        {
            Id = activity.Id,
            CreatedAt = activity.CreatedAt,
            Type = Enum.Parse<ActivityType>(activity.Type),
            PerformedBy = new ActivityPerformedBy
            {
                ActorType = activity.PerformedBy.ActorType,
                ActorName = activity.PerformedBy.ActorName,
                ActorId = activity.PerformedBy.ActorId
            },
            Description = activity.Description.Select(d => new ActivityDescription
            {
                Value = d.Value,
                LanguageCode = d.LanguageCode
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities?isSilentUpdate=true", 
            createDialogActivityRequest, 
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                if (errorContent.Contains("already exists"))
                {
                    logger.LogWarning(
                        "Activity already exists for notification {NotificationId} on correspondence {CorrespondenceId} and dialog {DialogId}", 
                        notificationId, 
                        correspondenceId, 
                        dialogId);
                    return;
                }
            }

            logger.LogError(
                "Failed to add activity for notification {NotificationId}. Response: {StatusCode}: {Content}", 
                notificationId,
                response.StatusCode, 
                errorContent);

            throw new HttpRequestException(
                $"Failed to add activity for notification {notificationId}. Status: {response.StatusCode}, Content: {errorContent}");
        }
        else
        {
            logger.LogInformation(
                "Successfully added notification activity for notification {NotificationId} on correspondence {CorrespondenceId}", 
                notificationId, 
                correspondenceId);
        }
    }

    /// <summary>
    /// Adds notification activities for a correspondence, checking for duplicates to prevent re-adding existing activities.
    /// Groups notifications by correspondence and only adds activities that don't already exist in the dialog.
    /// </summary>
    /// <param name="correspondenceId">The correspondence ID</param>
    /// <param name="notificationIds">List of notification IDs belonging to this correspondence</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task AddNotificationActivitiesWithDuplicateCheck(Guid correspondenceId, List<Guid> notificationIds, CancellationToken cancellationToken)
    {
        if (notificationIds == null || notificationIds.Count == 0)
        {
            return;
        }

        // Get all notifications in a single batch query
        var notifications = await correspondenceNotificationRepository.GetNotificationsByIds(notificationIds, cancellationToken);

        // Log warnings for any missing notifications
        if (notifications.Count < notificationIds.Count)
        {
            var foundIds = notifications.Select(n => n.Id).ToHashSet();
            var missingIds = notificationIds.Where(id => !foundIds.Contains(id));
            foreach (var missingId in missingIds)
            {
                logger.LogWarning("Notification with id {NotificationId} not found. Skipping.", missingId);
            }
        }

        if (notifications.Count == 0)
        {
            return;
        }

        // Validate that all notifications belong to the same correspondence
        var distinctCorrespondenceIds = notifications
            .Select(n => n.CorrespondenceId)
            .Distinct()
            .ToList();

        if (distinctCorrespondenceIds.Count > 1)
        {
            logger.LogError(
                "Notifications with mixed CorrespondenceIds detected. Expected all notifications to belong to correspondence {CorrespondenceId}, but found {Count} different correspondences. Notification IDs: {NotificationIds}",
                correspondenceId,
                distinctCorrespondenceIds.Count,
                string.Join(", ", notificationIds));
            throw new ArgumentException($"All notifications must belong to the same correspondence. Found {distinctCorrespondenceIds.Count} different correspondences.");
        }

        if (distinctCorrespondenceIds[0] != correspondenceId)
        {
            logger.LogError(
                "Notifications belong to correspondence {ActualCorrespondenceId} but expected correspondence {ExpectedCorrespondenceId}",
                distinctCorrespondenceIds[0],
                correspondenceId);
            throw new ArgumentException($"Notifications belong to correspondence {distinctCorrespondenceIds[0]} but expected {correspondenceId}");
        }

        var correspondence = notifications.First().Correspondence!;

        var dialogId = correspondence.ExternalReferences
            .FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)
            ?.ReferenceValue;

        if (dialogId is null)
        {
            if (correspondence.IsMigrating)
            {
                logger.LogWarning(
                    "Skipping adding notification activities for correspondence {CorrespondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", 
                    correspondence.Id);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondence.Id}");
        }

        // Fetch existing dialog with all activities
        CreateDialogRequest dialog;
        try
        {
            dialog = await GetDialog(dialogId);
        }
        catch (DialogNotFoundException)
        {
            logger.LogWarning(
                "Dialog {DialogId} not found for correspondence {CorrespondenceId}. Cannot add notification activities.",
                dialogId,
                correspondenceId);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to fetch dialog {DialogId} for correspondence {CorrespondenceId}. Cannot check for duplicate activities.",
                dialogId,
                correspondenceId);
            throw;
        }

        var existingActivities = dialog.Activities ?? new List<Activity>();
        var activitiesToAdd = new List<(Guid NotificationId, Activity Activity)>();

        // Build activities from notifications and check for duplicates
        foreach (var notification in notifications)
        {
            var activity = CreateDialogRequestMapper.GetActivityFromAltinn2Notification(correspondence, notification);

            // Check if this activity already exists
            bool isDuplicate = existingActivities.Any(existingActivity => 
                AreActivitiesEquivalent(existingActivity, activity));

            if (isDuplicate)
            {
                logger.LogInformation(
                    "Activity for notification {NotificationId} already exists in dialog {DialogId}. Skipping.",
                    notification.Id,
                    dialogId);
            }
            else
            {
                activitiesToAdd.Add((notification.Id, activity));
            }
        }

        // Add only new activities using the shared helper
        foreach (var (notificationId, activity) in activitiesToAdd)
        {
            await PostActivityToDialog(dialogId, notificationId, correspondenceId, activity, cancellationToken);
        }

        logger.LogInformation(
            "Processed {TotalCount} notifications for correspondence {CorrespondenceId}. Added {AddedCount} new activities, skipped {SkippedCount} duplicates.",
            notifications.Count,
            correspondenceId,
            activitiesToAdd.Count,
            notifications.Count - activitiesToAdd.Count);
    }

    /// <summary>
    /// Builds forwarding activities for migrated correspondence.
    /// Handles party lookups, DialogActivityId management, and mailbox supplier resolution.
    /// Returns complete Activity objects ready to be included in dialog creation.
    /// </summary>
    private async Task<List<Activity>> BuildForwardingActivities(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        var forwardingActivities = new List<Activity>();

        if (correspondence.ForwardingEvents == null || !correspondence.ForwardingEvents.Any())
        {
            return forwardingActivities;
        }

        foreach (var forwardingEvent in correspondence.ForwardingEvents.OrderBy(fe => fe.ForwardedOnDate))
        {
            try
            {
                var activity = await BuildForwardingActivity(forwardingEvent, correspondence, cancellationToken);
                if (activity != null)
                {
                    forwardingActivities.Add(activity);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to build forwarding activity for event {ForwardingEventId} on correspondence {CorrespondenceId}. Skipping this event.", 
                    forwardingEvent.Id, correspondence.Id);
            }
        }

        return forwardingActivities;
    }

    /// <summary>
    /// Builds a complete Activity object for a single forwarding event.
    /// Performs all necessary party lookups and determines forwarding type.
    /// </summary>
    private async Task<Activity> BuildForwardingActivity(CorrespondenceForwardingEventEntity forwardingEvent, CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        // Generate DialogActivityId if needed, but don't persist until validation succeeds
        Guid dialogActivityId;
        bool persistNewActivityId = false;
        if (forwardingEvent.DialogActivityId == null)
        {
            dialogActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
            persistNewActivityId = true;
        }
        else
        {
            dialogActivityId = forwardingEvent.DialogActivityId.Value;
        }

        // Resolve forwardedBy party
        var forwardedByParty = await altinnRegisterService
            .LookUpPartyById(forwardingEvent.ForwardedByPartyUuid.ToString(), cancellationToken);
        if (forwardedByParty == null)
        {
            throw new Exception($"Could not find party for ForwardedByPartyUuid {forwardingEvent.ForwardedByPartyUuid} in forwarding event {forwardingEvent.Id}");
        }

        string? forwardedByUrn;
        if (forwardedByParty is SelfIdentifiedUser)
        {
            // Special handling for self-identified users in dialog activities
            var externalUrn = forwardedByParty.GetExternalUrn();
            forwardedByUrn = await GetDialogActivityParty(externalUrn);
            if (string.IsNullOrWhiteSpace(forwardedByUrn))
            {
                forwardedByUrn = externalUrn;
            }
        }
        else
        {
            forwardedByUrn = forwardedByParty.GetExternalUrn();
            if (string.IsNullOrWhiteSpace(forwardedByUrn))
                throw new Exception($"Party type {forwardedByParty.GetType().Name} has no externalUrn for ForwardedByPartyUuid {forwardingEvent.ForwardedByPartyUuid} in forwarding event {forwardingEvent.Id}");
        }

        // Determine forwarding type and create appropriate activity
        DialogportenTextType textType;
        string[] tokens;

        if (forwardingEvent.ForwardedToUserUuid is not null)
        {
            // Instance delegation
            var forwardedToUser = await altinnRegisterService.LookUpPartyById(forwardingEvent.ForwardedToUserUuid.Value.ToString(), cancellationToken);
            if (forwardedToUser == null)
            {
                throw new Exception($"Could not find party for ForwardedToUserUuid {forwardingEvent.ForwardedToUserUuid} in forwarding event {forwardingEvent.Id}");
            }
            textType = DialogportenTextType.CorrespondenceInstanceDelegated;
            tokens = new[]
            {
                correspondence.Content?.MessageTitle ?? string.Empty,
                forwardedToUser.GetDisplayName() ?? throw new Exception($"No name found for user {forwardedToUser.Uuid}"),
                forwardingEvent.ForwardingText ?? string.Empty
            };
        }
        else if (!string.IsNullOrEmpty(forwardingEvent.ForwardedToEmailAddress))
        {
            // Email forwarding
            textType = DialogportenTextType.CorrespondenceForwardedToEmail;
            tokens = new[]
            {
                correspondence.Content?.MessageTitle ?? string.Empty,
                forwardingEvent.ForwardedToEmailAddress,
                forwardingEvent.ForwardingText ?? string.Empty
            };
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
            textType = DialogportenTextType.CorrespondenceForwardedToMailboxSupplier;
            tokens = new[]
            {
                correspondence.Content?.MessageTitle ?? string.Empty,
                mailboxSupplierName,
                forwardingEvent.ForwardingText ?? string.Empty
            };
        }
        else
        {
            throw new Exception($"Forwarding event {forwardingEvent.Id} has no valid forwarding target (no ForwardedToUserUuid, ForwardedToEmailAddress or MailboxSupplier)");
        }

        // All validation and lookups succeeded, now persist the new DialogActivityId if needed
        if (persistNewActivityId)
        {
            await correspondenceForwardingEventRepository.SetDialogActivityId(forwardingEvent.Id, dialogActivityId, cancellationToken);
            forwardingEvent.DialogActivityId = dialogActivityId;
        }

        var performedBy = new PerformedBy
        {
            // ActorType is always "PartyRepresentative" for forwarding events since they are
            // performed by a user forwarding on behalf of a party.
            ActorType = "PartyRepresentative"
        };
        if (!string.IsNullOrWhiteSpace(forwardedByUrn))
        {
            performedBy.ActorId = forwardedByUrn;
        }

        // Build and return the Activity object
        return new Activity
        {
            Id = dialogActivityId.ToString(),
            PerformedBy = performedBy,
            CreatedAt = forwardingEvent.ForwardedOnDate,
            Type = "Information",
            Description = new List<Description>
            {
                new()
                {
                    LanguageCode = "nb",
                    Value = DialogportenText.GetDialogportenText(textType, DialogportenLanguageCode.NB, tokens)
                },
                new()
                {
                    LanguageCode = "nn",
                    Value = DialogportenText.GetDialogportenText(textType, DialogportenLanguageCode.NN, tokens)
                },
                new()
                {
                    LanguageCode = "en",
                    Value = DialogportenText.GetDialogportenText(textType, DialogportenLanguageCode.EN, tokens)
                }
            }
        };
    }

    private async Task<string?> GetDialogParty(CorrespondenceEntity correspondence)
    {
        var dialogParty = correspondence.GetRecipientUrn();
        // Migrated self-identified
        if (dialogParty?.StartsWith(UrnConstants.PartyUuid) == true)
        {
            var recipientParty = await altinnRegisterService.LookUpPartyById(correspondence.Recipient.WithUrnPrefix(), cancellationToken: CancellationToken.None);
            var recipientUsername = recipientParty?.GetUsername();
            if (recipientParty is null || recipientUsername is null)
            {
                throw new Exception($"Could not find recipient party in Altinn Register for self-identified correspondence with recipient urn {correspondence.Recipient.WithUrnPrefix()}");
            }
            dialogParty = $"{UrnConstants.PersonLegacySelfIdentifiedAttribute}:{recipientUsername}";
        }
        return dialogParty;
    }

    private async Task<string?> GetDialogActivityParty(string? dialogParty)
    {
        if (dialogParty is null)
        {
            return null;
        }
        // Migrated self-identified
        if (dialogParty.StartsWith(UrnConstants.PartyUuid) || dialogParty.StartsWith(UrnConstants.Party))
        {
            var recipientParty = await altinnRegisterService.LookUpPartyById(dialogParty, cancellationToken: CancellationToken.None);
            if (recipientParty == null)
            {
                throw new Exception($"Could not find recipient party in Altinn Register for self-identified correspondence with recipient urn {dialogParty}");
            }
            var recipientUsername = recipientParty.GetUsername();
            if (recipientUsername is not null)
            {
                return $"{UrnConstants.PersonLegacySelfIdentifiedAttribute}:{recipientUsername}";
            }
            var externalUrn = recipientParty.GetExternalUrn();
            if (externalUrn is not null)
            {
                return externalUrn;
            }
            throw new Exception($"Could not find recipient party in Altinn Register for self-identified correspondence with recipient urn {dialogParty}");
        }
        return dialogParty;
    }

    /// <summary>
    /// Compares two activities to determine if they are semantically equivalent.
    /// Ignores the Id field since it's not idempotent for migrated activities.
    /// </summary>
    /// <param name="activity1">First activity to compare (from dialog)</param>
    /// <param name="activity2">Second activity to compare (from notification)</param>
    /// <returns>True if activities are equivalent, false otherwise</returns>
    private bool AreActivitiesEquivalent(Activity activity1, Activity activity2)
    {
        // Compare CreatedAt (within 1 second tolerance for timestamp precision)
        if (Math.Abs((activity1.CreatedAt - activity2.CreatedAt).TotalSeconds) > 1)
        {
            return false;
        }

        // Compare Type
        if (activity1.Type != activity2.Type)
        {
            return false;
        }

        // Compare PerformedBy
        if (!ArePerformedByEquivalent(activity1.PerformedBy, activity2.PerformedBy))
        {
            return false;
        }

        // Compare Description
        if (!AreDescriptionsEquivalent(activity1.Description, activity2.Description))
        {
            return false;
        }

        return true;
    }

    private bool ArePerformedByEquivalent(PerformedBy? pb1, PerformedBy? pb2)
    {
        if (pb1 == null && pb2 == null) return true;
        if (pb1 == null || pb2 == null) return false;

        return pb1.ActorType == pb2.ActorType &&
               pb1.ActorName == pb2.ActorName &&
               pb1.ActorId == pb2.ActorId;
    }

    private bool AreDescriptionsEquivalent(List<Description>? desc1, List<Description>? desc2)
    {
        if (desc1 == null && desc2 == null) return true;
        if (desc1 == null || desc2 == null) return false;
        if (desc1.Count != desc2.Count) return false;

        // Compare descriptions as sets (order doesn't matter)
        foreach (var d1 in desc1)
        {
            if (!desc2.Any(d2 => d2.Value == d1.Value && d2.LanguageCode == d1.LanguageCode))
            {
                return false;
            }
        }

        return true;
    }
}
