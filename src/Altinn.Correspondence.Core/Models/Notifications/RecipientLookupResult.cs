using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications;

public class RecipientLookupResult
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipientLookupStatus Status { get; set; }

    public List<string>? IsReserved { get; set; }

    public List<string>? MissingContact { get; set; }
}

/// <summary>
/// Enum describing the success rate for recipient lookup
/// </summary>
public enum RecipientLookupStatus
{
    /// <summary>
    /// The recipient lookup was successful for all recipients
    /// </summary>
    Success,

    /// <summary>
    /// The recipient lookup was successful for some recipients
    /// </summary>
    PartialSuccess,

    /// <summary>
    /// The recipient lookup failed for all recipients
    /// </summary>
    Failed
}