using System.Text.Json.Serialization;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

public class SetDialogSystemLabelRequest
{
    [JsonPropertyName("dialogId")]
    public Guid DialogId { get; set; }

    [JsonPropertyName("performedBy")]
    public Actor? PerformedBy { get; set; }

    [JsonPropertyName("addLabels")]
    public IReadOnlyCollection<SystemLabel> AddLabels { get; set; } = Array.Empty<SystemLabel>();

    [JsonPropertyName("removeLabels")]
    public IReadOnlyCollection<SystemLabel> RemoveLabels { get; set; } = Array.Empty<SystemLabel>();
}

public class Actor
{
    [JsonPropertyName("actorType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DialogportenActorType ActorType { get; set; }

    [JsonPropertyName("actorName")]
    public string? ActorName { get; set; }

    [JsonPropertyName("actorId")]
    public string? ActorId { get; set; }
}

// Enum for system labels (match API contract)
public enum SystemLabel
{
    Default = 1,
    Bin = 2,
    Archive = 3,
    MarkedAsUnopened = 4,
    Sent = 5
}