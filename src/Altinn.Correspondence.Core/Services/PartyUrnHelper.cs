using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Register.Contracts;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Core.Services;

/// <summary>
/// Helper class for converting v2 <see cref="Party"/> entities to Dialogporten-compatible URN strings.
/// Centralizes the logic for party URN conversion to avoid duplication across the codebase.
/// </summary>
public class PartyUrnHelper
{
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly ILogger<PartyUrnHelper> _logger;

    public PartyUrnHelper(IAltinnRegisterService altinnRegisterService, ILogger<PartyUrnHelper> logger)
    {
        _altinnRegisterService = altinnRegisterService;
        _logger = logger;
    }

    /// <summary>
    /// Looks up a party by PartyUuid and converts it to a Dialogporten-compatible URN string.
    /// Returns null if party is not found.
    /// </summary>
    /// <param name="partyUuid">The PartyUuid to lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URN string or null if party not found</returns>
    public async Task<string?> GetPartyUrnByUuid(Guid partyUuid, CancellationToken cancellationToken)
    {
        var party = await _altinnRegisterService.LookUpPartyById(partyUuid.ToString(), cancellationToken);
        if (party is null)
        {
            _logger.LogWarning("Party with UUID {PartyUuid} not found in Altinn Register", partyUuid);
            return null;
        }

        var urn = party.GetExternalUrn();
        if (string.IsNullOrWhiteSpace(urn))
        {
            _logger.LogWarning("Party {PartyUuid} of type {PartyType} has no externalUrn", partyUuid, party.GetType().Name);
            return null;
        }
        return urn;
    }

    /// <summary>
    /// Looks up multiple parties by their PartyUuids and returns a dictionary mapping PartyUuid to URN.
    /// Parties that cannot be found or converted are logged and skipped.
    /// </summary>
    /// <param name="partyUuids">Collection of PartyUuids to lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping PartyUuid to URN string</returns>
    public async Task<Dictionary<Guid, string>> GetPartyUrnsByUuids(IEnumerable<Guid> partyUuids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        var distinctUuids = partyUuids.Distinct().ToList();

        foreach (var uuid in distinctUuids)
        {
            var urn = await GetPartyUrnByUuid(uuid, cancellationToken);
            if (urn != null)
            {
                result[uuid] = urn;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets Dialogporten actor IDs for status events.
    /// Looks up parties for Read and Confirmed status events and converts them to URNs.
    /// </summary>
    /// <param name="statusEvents">List of status events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping PartyUuid to URN string</returns>
    public async Task<Dictionary<Guid, string>> GetDialogPortenActorIdsForStatusEvents(List<CorrespondenceStatusEntity> statusEvents, CancellationToken cancellationToken)
    {
        var partyUuidsToLookup = statusEvents
            .Where(e => e.Status == CorrespondenceStatus.Read || e.Status == CorrespondenceStatus.Confirmed)
            .Select(e => e.PartyUuid)
            .Distinct();

        return await GetPartyUrnsByUuids(partyUuidsToLookup, cancellationToken);
    }

    /// <summary>
    /// Gets Dialogporten actor IDs for status and deletion events.
    /// Combines PartyUuids from both event types and looks them up.
    /// </summary>
    /// <param name="statusEventsToExecute">List of status events</param>
    /// <param name="deletionEventsToExecute">List of deletion events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping PartyUuid to URN string</returns>
    public async Task<Dictionary<Guid, string>> GetDialogPortenActorIdsForEvents(
        List<CorrespondenceStatusEntity>? statusEventsToExecute,
        List<CorrespondenceDeleteEventEntity>? deletionEventsToExecute,
        CancellationToken cancellationToken)
    {
        var partyUuidsToLookup = (statusEventsToExecute ?? Enumerable.Empty<CorrespondenceStatusEntity>())
            .Select(e => e.PartyUuid)
            .Concat((deletionEventsToExecute ?? Enumerable.Empty<CorrespondenceDeleteEventEntity>())
                .Select(e => e.PartyUuid))
            .Distinct();

        return await GetPartyUrnsByUuids(partyUuidsToLookup, cancellationToken);
    }
}
