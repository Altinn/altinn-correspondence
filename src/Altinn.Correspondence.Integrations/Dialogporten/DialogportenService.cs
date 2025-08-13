using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten;

public class DialogportenService(HttpClient _httpClient, ICorrespondenceRepository _correspondenceRepository, IOptions<GeneralSettings> generalSettings, ILogger<DialogportenService> logger, IIdempotencyKeyRepository _idempotencyKeyRepository) : IDialogportenService
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

        logger.LogInformation("CreateCorrespondenceDialog for correspondence {correspondenceId}", correspondence.Id);

        // Create idempotency key for open dialog activity
        await CreateIdempotencyKeysForCorrespondence(correspondence, cancellationToken);

        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, generalSettings.Value.CorrespondenceBaseUrl);
        var response = await _httpClient.PostAsJsonAsync("dialogporten/api/v1/serviceowner/dialogs", createDialogRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var dialogResponse = await response.Content.ReadFromJsonAsync<string>(cancellationToken);
        if (dialogResponse is null)
        {
            throw new Exception("Dialogporten did not return a dialogId");
        }
        return dialogResponse;
    }

    public async Task PatchCorrespondenceDialogToConfirmed(Guid correspondenceId)
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
                logger.LogWarning("Skipping patching correspondence {correspondenceId} to confirmed as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
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
            logger.LogInformation("No actions to remove from dialog {dialogId} for correspondence {correspondenceId}", dialogId, correspondenceId);
            return;
        }
        var response = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", patchRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
    
    public async Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, DateTimeOffset activityTimestamp, params string[] tokens)
    {
        logger.LogInformation("CreateInformationActivity {actorType}: {textType} for correspondence {correspondenceId}",
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
            if (correspondence.Altinn2CorrespondenceId.GetValueOrDefault() > 0)
            {
                logger.LogWarning("Skipping creating information activity for {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, textType, ActivityType.Information, activityTimestamp, tokens);

        if (textType == DialogportenTextType.CorrespondenceConfirmed)
        {
            if (correspondence.Statuses.Count(s => s.Status == CorrespondenceStatus.Confirmed) >= 2)
            {
                logger.LogInformation("Correspondence with id {correspondenceId} already has a Confirmed status, skipping activity creation on Dialogporten", correspondenceId);
                return;
            }
            // Get the pre-created idempotency key for confirm activity
            var existingConfirmIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
                correspondence.Id,
                null, // No attachment for confirm activity
                StatusAction.Confirmed,
                IdempotencyType.DialogportenActivity,
                cancellationToken);

            if (existingConfirmIdempotencyKey != null)
            {
                createDialogActivityRequest.Id = existingConfirmIdempotencyKey.Id.ToString(); // Use the pre-created activity ID
            }
        }

        // Only set activity ID for download events using the stored idempotency key
        if (textType == DialogportenTextType.DownloadStarted)
        {
            if (tokens.Length < 2 || !Guid.TryParse(tokens[1], out var attachmentId))
            {
                logger.LogError("Invalid attachment ID token for download activity on correspondence {correspondenceId}", correspondenceId);
                throw new ArgumentException("Invalid attachment ID token", nameof(tokens));
            }

            if (correspondence.Statuses.Count(s => s.Status == CorrespondenceStatus.AttachmentsDownloaded && s.StatusText.Contains(attachmentId.ToString())) >= 2)
            {
                logger.LogInformation("Correspondence with id {correspondenceId} already has an AttachmentsDownloaded status for attachment {attachmentId}, skipping activity creation on Dialogporten", correspondenceId, attachmentId);
                return;
            }

            var existingIdempotencyKey = await _idempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync(
                correspondence.Id,
                attachmentId,
                StatusAction.AttachmentDownloaded,
                IdempotencyType.DialogportenActivity,
                cancellationToken);

            if (existingIdempotencyKey != null)
            {
                createDialogActivityRequest.Id = existingIdempotencyKey.Id.ToString();
            }
        }

        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities", createDialogActivityRequest, cancellationToken);
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

    public async Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp)
    {
        logger.LogInformation("CreateOpenedActivity by {actorType} for correspondence {correspondenceId}",
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

        if (correspondence.Statuses.Count(s => s.Status == CorrespondenceStatus.Fetched) >= 2)
        {
            logger.LogInformation("Correspondence with id {correspondenceId} already has a Fetched status, skipping activity creation on Dialogporten", correspondenceId);
            return;
        }

        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            if (correspondence.Altinn2CorrespondenceId.GetValueOrDefault() > 0)
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
                    StatusAction = StatusAction.Fetched,
                    IdempotencyType = IdempotencyType.DialogportenActivity
                },
                cancellationToken);
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, ActivityType.DialogOpened, activityTimestamp);
        createDialogActivityRequest.Id = existingOpenIdempotencyKey.Id.ToString(); // Use the created activity ID
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities", createDialogActivityRequest, cancellationToken);
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
            if (correspondence.Altinn2CorrespondenceId.GetValueOrDefault() > 0)
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

    public async Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName, DateTimeOffset activityTimestamp)
    {
        logger.LogInformation("CreateCorrespondencePurgedActivity by {actorType}: for correspondence {correspondenceId}",
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
            if (correspondence.Altinn2CorrespondenceId.GetValueOrDefault() > 0)
            {
                logger.LogWarning("Skipping creating dialog purged activity for correspondence {correspondenceId} as it is an Altinn2 correspondence without Dialogporten dialog", correspondenceId);
                return;
            }
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, Models.ActivityType.DialogDeleted, activityTimestamp);
        if (actorType != DialogportenActorType.ServiceOwner)
        {
            createDialogActivityRequest.PerformedBy.ActorName = actorName;
            createDialogActivityRequest.PerformedBy.ActorId = null;
        }
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task<CreateDialogRequest> GetDialog(string dialogId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var response = await _httpClient.GetAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", cancellationToken);
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

    private async Task CreateIdempotencyKeysForCorrespondence(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
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
        if (correspondence.IsConfirmationNeeded)
        {
            var confirmActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
            var confirmIdempotencyKey = new IdempotencyKeyEntity
            {
                Id = confirmActivityId,
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
            var downloadActivityId = Uuid.NewDatabaseFriendly(UUIDNext.Database.PostgreSql);
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
    }


    #region MigrationRelated    
    /// <summary>
    /// Create Dialog in Dialogportern without creating any events. Used in regards to old correspondences being migrated from Altinn 2 to Altinn 3.
    /// </summary>
    public async Task<string> CreateCorrespondenceDialogForMigratedCorrespondence(Guid correspondenceId, CorrespondenceEntity? correspondence, bool enableEvents = false)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        if (correspondence is null)
        {
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        await CreateIdempotencyKeysForCorrespondence(correspondence, cancellationToken);

        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, generalSettings.Value.CorrespondenceBaseUrl, true);
        string updateType = enableEvents ? "" : "?IsSilentUpdate=true";
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs{updateType}", createDialogRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
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
    /// Create a batch of migrated correspondence for Dialogporten.
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public async Task CreateBatchOfMigratedCorrespondenceForDialogporten(int size)
    {
        // Use ApplicationDbContext
        // Get all correspondences that have been migrated from Altinn 2 and has no associated external reference indicating that it already has a dialog
        // Schedule background job for these
    }
    #endregion
}
