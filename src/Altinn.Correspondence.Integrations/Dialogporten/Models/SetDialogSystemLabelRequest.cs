using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

public class SetDialogSystemLabelRequest
{
    [JsonPropertyName("dialogId")]
    public Guid DialogId { get; set; }

    [JsonPropertyName("enduserId")]
    public string EnduserId { get; set; } = string.Empty;

    [JsonPropertyName("addLabels")]
    public IReadOnlyCollection<SystemLabel> AddLabels { get; set; } = Array.Empty<SystemLabel>();

    [JsonPropertyName("removeLabels")]
    public IReadOnlyCollection<SystemLabel> RemoveLabels { get; set; } = Array.Empty<SystemLabel>();
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