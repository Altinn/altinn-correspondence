using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten;

public class DialogportenService(HttpClient _httpClient, ICorrespondenceRepository _correspondenceRepository, IAltinnRegisterService _altinnRegisterService, ILogger<DialogportenService> _logger) : IDialogportenService
{
    public async Task<string> CreateCorrespondenceDialog(Guid correspondenceId, CancellationToken cancellationToken = default)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            _logger.LogError("Correspondence with id {correspondenceId} not found", correspondenceId);
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var partyId = await _altinnRegisterService.LookUpPartyId(correspondence.Sender, cancellationToken);
        if (partyId is null)
        {
            throw new ArgumentException($"Could not find partyId for organization {correspondence.Sender}", nameof(correspondence.Sender));
        }

        var dialogId = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(); // Dialogporten requires time-stamped GUIDs, not supported natively until .NET 9.0
        var createDialogRequest = CreateCorrespondenceDialogMapper.CreateCorrespondenceDialog(correspondence, correspondence.Recipient.Replace("0192:", ""), dialogId);
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
}
