using System.Globalization;
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
        var fromApi = ReadExternalUrn(party);
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

    /// <summary>
    /// Reads the Register API's <c>externalUrn</c> field. Since Altinn.Register.Contracts 1.7.0 this is a
    /// typed property; before that it was only reachable through <c>JsonExtensionData</c>. The value is
    /// wrapped in <c>NonExhaustive</c>, so URN schemes unknown to the package still round-trip verbatim.
    /// Returns null when the field was not requested or not populated in the response.
    /// </summary>
    private static string? ReadExternalUrn(Party party)
        => party.ExternalUrn.HasValue ? party.ExternalUrn.Value.ToString() : null;

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
        var isEmail = username.Contains('@', StringComparison.Ordinal);
        var prefix = isEmail
            ? UrnConstants.PersonIdPortenEmailAttribute
            : UrnConstants.PersonLegacySelfIdentifiedAttribute;
        var normalizedUsername = isEmail ? username.ToLowerInvariant() : username;

        return $"{prefix}:{normalizedUsername}";
    }
}
