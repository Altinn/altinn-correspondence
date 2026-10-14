using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Services;
using Altinn.Register.Contracts;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public static class PerformedByResolver
{
    public static async Task<Dictionary<Guid, PerformedBy>> ResolveAsync(
        IAltinnRegisterService altinnRegisterService,
        IEnumerable<Guid> partyUuids,
        string sender,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var distinctPartyUuids = partyUuids.Where(partyUuid => partyUuid != Guid.Empty).Distinct().ToList();
        if (distinctPartyUuids.Count == 0)
        {
            return new Dictionary<Guid, PerformedBy>();
        }

        List<Party>? parties;
        try
        {
            parties = await altinnRegisterService.LookUpPartiesByIds(
                distinctPartyUuids.Select(partyUuid => partyUuid.ToString()).ToList(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to look up parties for correspondence status events");
            return new Dictionary<Guid, PerformedBy>();
        }

        var performedByByPartyUuid = new Dictionary<Guid, PerformedBy>();
        foreach (var party in parties ?? [])
        {
            if (party.Uuid == Guid.Empty)
            {
                continue;
            }

            performedByByPartyUuid[party.Uuid] = Create(party, sender);
        }

        var unresolvedPartyUuids = distinctPartyUuids.Where(partyUuid => !performedByByPartyUuid.ContainsKey(partyUuid)).ToList();
        if (unresolvedPartyUuids.Count > 0)
        {
            logger.LogWarning(
                "Could not resolve performedBy for status event party UUIDs {PartyUuids}",
                string.Join(", ", unresolvedPartyUuids));
        }

        return performedByByPartyUuid;
    }

    /// <summary>
    /// The sender party is the service owner. Any other party is someone representing a party.
    /// </summary>
    public static PerformedBy Create(Party party, string sender)
    {
        return new PerformedBy
        {
            ActorType = IsServiceOwner(party, sender)
                ? PerformedByActorType.ServiceOwner
                : PerformedByActorType.PartyRepresentative,
            ActorName = party.GetDisplayName(),
            ActorId = party.GetExternalUrn()
        };
    }

    private static bool IsServiceOwner(Party party, string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var externalUrn = party.GetExternalUrn();
        if (!string.IsNullOrEmpty(externalUrn)
            && string.Equals(externalUrn, sender, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var organizationIdentifier = party.GetOrganizationIdentifier();
        return !string.IsNullOrEmpty(organizationIdentifier)
            && string.Equals(organizationIdentifier, sender.WithoutPrefix(), StringComparison.OrdinalIgnoreCase);
    }
}
