using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications;

public class RecipientLookupResult
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipientLookupStatus Status { get; set; }

    public List<string>? IsReserved { get; set; }

    public List<string>? MissingContact { get; set; }
}

public enum RecipientLookupStatus
{
    Success,

    PartialSuccess,

    Failed
}