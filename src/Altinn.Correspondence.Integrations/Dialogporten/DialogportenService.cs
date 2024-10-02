using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Dialogporten;

public class DialogportenService(HttpClient _httpClient, ICorrespondenceRepository _correspondenceRepository, IAltinnRegisterService _altinnRegisterService, IOptions<AltinnOptions> altinnOptions, ILogger<DialogportenService> _logger) : IDialogportenService
{
    public async Task<string> CreateCorrespondenceDialog(Guid correspondenceId, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            _logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, altinnOptions.Value.PlatformGatewayUrl);
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

    public async Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, string description, string? extendedType = null, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            _logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
        if (dialogId is null)
        {
            throw new ArgumentException($"No dialog found on on correspondence with id {correspondenceId}");
        }

        var createDialogActivityRequest = CreateDialogActivityRequestMapper.CreateDialogActivityRequest(correspondence, actorType, description, extendedType, ActivityType.Information);
        var response = await _httpClient.PostAsJsonAsync($"dialogporten/api/v1/serviceowner/dialogs/{dialogId}/activities", createDialogActivityRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Response from Dialogporten was not successful: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}
