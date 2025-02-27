
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models;
/// <summary>
/// Represents a correspondence that has been initialized
/// </summary>
public class InitializedCorrespondencesExt
{
    /// <summary>
    /// The ID of the correspondence
    /// </summary>
    [JsonPropertyName("correspondenceId")]
    public Guid CorrespondenceId { get; set; }

    /// <summary>
    /// The current status of the correspondence
    /// </summary>
    [JsonPropertyName("status")]
    public CorrespondenceStatusExt Status { get; set; }

    /// <summary>
    /// The recipient of the correspondence
    /// </summary>
    [JsonPropertyName("recipient")]
    public required string Recipient { get; set; }

    /// <summary>
    /// Information about the notifications that were created for the correspondence
    /// </summary>
    [JsonPropertyName("notifications")]
    public List<InitializedCorrespondencesNotificationsExt>? Notifications { get; set; }
}