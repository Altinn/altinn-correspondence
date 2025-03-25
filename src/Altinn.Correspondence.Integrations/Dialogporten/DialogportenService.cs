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

namespace Altinn.Correspondence.Integrations.Dialogporten;

public class DialogportenService(HttpClient _httpClient, ICorrespondenceRepository _correspondenceRepository, IOptions<GeneralSettings> generalSettings, ILogger<DialogportenService> logger) : IDialogportenService
{
    public async Task<string> CreateCorrespondenceDialog(Guid correspondenceId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

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
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }
        var patchRequest = new DialogPatchRequestBuilder();
        var dialog = await GetDialog(dialogId);
        string confirmEndpointUrl = $"{generalSettings.Value.CorrespondenceBaseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondence.Id}/confirm";
        var guiActionIndexToDelete = dialog.GuiActions.FindIndex(dialog => dialog.Url == confirmEndpointUrl);
        var apiActionIndexToDelete = dialog.ApiActions.FindIndex(dialog => dialog.Endpoints.Any(endpoint => endpoint.Url == confirmEndpointUrl));
        if (guiActionIndexToDelete != -1)
        {
            patchRequest.WithRemoveGuiActionOperation(guiActionIndexToDelete);
        }
        if (apiActionIndexToDelete != -1)
        {
            patchRequest.WithRemoveApiActionOperation(apiActionIndexToDelete);
        }
        var response = await _httpClient.PatchAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}", patchRequest.Build(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, params string[] tokens)
    {
        logger.LogInformation("CreateInformationActivity {actorType}: {textType} for correspondence {correspondenceId}",
            Enum.GetName(typeof(DialogportenActorType), actorType),
            Enum.GetName(typeof(DialogportenTextType), textType),
            correspondenceId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            throw new ArgumentException($"No dialog found on on correspondence with id {correspondenceId}");
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, textType, Models.ActivityType.Information, tokens);
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public async Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType)
    {
        logger.LogInformation("CreateOpenedActivity by {actorType} for correspondence {correspondenceId}",
            Enum.GetName(typeof(DialogportenActorType), actorType),
            correspondenceId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            throw new ArgumentException($"No dialog found on correspondence with id {correspondenceId}");
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, null, Models.ActivityType.DialogOpened);
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
    public async Task PurgeCorrespondenceDialog(Guid correspondenceId)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            throw new ArgumentException($"No dialog found on on correspondence with id {correspondenceId}");
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
}
