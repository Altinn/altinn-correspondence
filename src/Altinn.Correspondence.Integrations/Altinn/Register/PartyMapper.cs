using System.Globalization;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Register;

namespace Altinn.Correspondence.Integrations.Altinn.Register;

/// <summary>
/// Mapper for converting between V1 and V2 Party models
/// </summary>
public static class PartyMapper
{
    /// <summary>
    /// Maps a V2 Party to a V1 Party model
    /// </summary>
    public static Party MapToV1(PartyV2 partyV2)
    {
        // Normalize name to title case to match original behavior
        string? normalizedName = null;
        if (!string.IsNullOrWhiteSpace(partyV2.DisplayName))
        {
            normalizedName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(partyV2.DisplayName.ToLower());
        }

        var party = new Party
        {
            PartyId = partyV2.PartyId != null ? (int)partyV2.PartyId.Value : 0,
            PartyUuid = partyV2.PartyUuid,
            Name = normalizedName,
            IsDeleted = partyV2.IsDeleted == true,
            Resources = new List<string>(),
            SSN = partyV2.PersonIdentifier,
            OrgNumber = partyV2.OrganizationIdentifier
        };

        if (!string.IsNullOrWhiteSpace(partyV2.PartyType))
        {
            if (Enum.TryParse<PartyType>(partyV2.PartyType, true, out var parsedType))
            {
                party.PartyTypeName = parsedType;
            }
        }

        return party;
    }

    /// <summary>
    /// Maps a list of V2 Parties to V1 Party models
    /// </summary>
    public static List<Party> MapListToV1(List<PartyV2> partiesV2)
    {
        return partiesV2.Select(MapToV1).ToList();
    }
}
