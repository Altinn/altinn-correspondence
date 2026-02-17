using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Register;

/// <summary>
/// Represents a list response object from the Register API
/// </summary>
/// <typeparam name="T">The type of items in the list</typeparam>
public class ListObject<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();
}

/// <summary>
/// Represents user information for a party
/// </summary>
public class PartyUser
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }
}

/// <summary>
/// V2 Party model from Register API
/// </summary>
public class PartyV2
{
    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    [JsonPropertyName("partyUuid")]
    public Guid PartyUuid { get; set; }

    /// <summary>
    /// Gets the version ID of the party.
    /// </summary>
    [JsonPropertyName("versionId")]
    public ulong VersionId { get; set; }

    /// <summary>
    /// Gets the canonical URN of the party.
    /// </summary>
    [JsonPropertyName("urn")]
    public string? Urn { get; set; }

    /// <summary>
    /// Gets the external reference of the party.
    /// </summary>
    [JsonPropertyName("externalUrn")]
    public string? ExternalUrn { get; set; }

    /// <summary>
    /// Gets the ID of the party.
    /// </summary>
    [JsonPropertyName("partyId")]
    public uint? PartyId { get; set; }

    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    [JsonPropertyName("partyType")]
    public string? PartyType { get; set; }

    /// <summary>
    /// Gets the display-name of the party.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets the person identifier of the party, or null if the party is not a person.
    /// </summary>
    [JsonPropertyName("personIdentifier")]
    public string? PersonIdentifier { get; set; }

    /// <summary>
    /// Gets the organization identifier of the party, or null if the party is not an organization.
    /// </summary>
    [JsonPropertyName("organizationIdentifier")]
    public string? OrganizationIdentifier { get; set; }

    /// <summary>
    /// Gets when the party was created in Altinn 3.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets when the party was last modified in Altinn 3.
    /// </summary>
    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Gets whether the party is deleted.
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public bool? IsDeleted { get; set; }

    /// <summary>
    /// Gets when the party was deleted.
    /// </summary>
    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets user information for the party.
    /// </summary>
    [JsonPropertyName("user")]
    public PartyUser? User { get; set; }
}
