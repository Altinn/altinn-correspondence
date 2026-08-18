using System.Globalization;
using System.Text.Json;
using Altinn.Authorization.ModelUtils;
using Altinn.Correspondence.Common.Constants;
using Altinn.Register.Contracts;

namespace Altinn.Correspondence.Core.Extensions;

/// <summary>
/// Accessors that smooth over the polymorphic v2 <see cref="Party"/> model from
/// Altinn.Register.Contracts: identifiers live on subclasses (Person / Organization)
/// and most scalar properties are wrapped in FieldValue&lt;T&gt;.
/// </summary>
public static class PartyExtensions
{
    public static string? GetPersonIdentifier(this Party party)
        => party is Person p ? p.PersonIdentifier.ToString() : null;

    public static string? GetOrganizationIdentifier(this Party party)
        => party is Organization o ? o.OrganizationIdentifier.ToString() : null;

    public static string? GetDisplayName(this Party party)
    {
        if (!party.DisplayName.HasValue)
        {
            return null;
        }

        var name = party.DisplayName.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }

    public static string? GetUnitType(this Party party)
        => party is Organization o && o.UnitType.HasValue ? o.UnitType.Value : null;

    /// <summary>
    /// Returns the party's numeric PartyId. Throws if the field was not populated in the response, since it should be.
    /// </summary>
    public static int GetPartyId(this Party party)
        => party.PartyId.HasValue
            ? (int)party.PartyId.Value
            : throw new InvalidOperationException($"Party {party.Uuid} has no PartyId");

    public static bool GetIsDeleted(this Party party)
        => party.IsDeleted.HasValue && party.IsDeleted.Value;

    public static int? GetUserId(this Party party)
    {
        if (!party.User.HasValue || party.User.Value is not { } user)
        {
            return null;
        }

        return user.UserId.HasValue ? (int)user.UserId.Value : null;
    }

    public static string? GetUsername(this Party party)
    {
        if (!party.User.HasValue || party.User.Value is not { } user)
        {
            return null;
        }

        if (!user.Username.HasValue || string.IsNullOrEmpty(user.Username.Value))
        {
            return null;
        }

        return Uri.EscapeDataString(user.Username.Value.ToLowerInvariant());
    }

    /// <summary>
    /// Returns the typed identifier URN for the party (e.g. person SSN or organization number URN).
    /// Uses the Register API's <c>externalUrn</c> field when present, otherwise derives the URN from typed party fields.
    /// Returns null for unsupported party types (SystemUser, EnterpriseUser).
    /// </summary>
    public static string? GetExternalUrn(this Party party)
    {
        var fromApi = ReadExternalUrnFromExtensionData(party);
        if (!string.IsNullOrEmpty(fromApi))
        {
            return fromApi;
        }

        return party switch
        {
            Person person => PartyUrn.PersonId.Create(person.PersonIdentifier).ToString(),
            Organization organization => PartyUrn.OrganizationId.Create(organization.OrganizationIdentifier).ToString(),
            SelfIdentifiedUser selfIdentifiedUser => BuildSelfIdentifiedExternalUrn(selfIdentifiedUser),
            _ => null,
        };
    }

    private static string? ReadExternalUrnFromExtensionData(Party party)
    {
        if (party is not IHasExtensionData { JsonExtensionData: { ValueKind: JsonValueKind.Object } extensionData })
        {
            return null;
        }

        if (!extensionData.TryGetProperty("externalUrn", out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString();
    }

    private static string? BuildSelfIdentifiedExternalUrn(SelfIdentifiedUser party)
    {
        if (!party.User.HasValue || party.User.Value is not { } user)
        {
            return null;
        }

        if (!user.Username.HasValue || string.IsNullOrWhiteSpace(user.Username.Value))
        {
            return null;
        }

        var username = user.Username.Value;
        var prefix = username.Contains('@', StringComparison.Ordinal)
            ? UrnConstants.PersonIdPortenEmailAttribute
            : UrnConstants.PersonLegacySelfIdentifiedAttribute;

        return $"{prefix}:{username}";
    }
}
