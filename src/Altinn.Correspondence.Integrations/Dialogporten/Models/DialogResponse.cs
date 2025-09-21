using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

public class DialogResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("serviceResource")]
    public string ServiceResource { get; set; }

    [JsonPropertyName("party")]
    public string Party { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }

    [JsonPropertyName("process")]
    public string? Process { get; set; }

    [JsonPropertyName("extendedStatus")]
    public string? ExtendedStatus { get; set; }

    [JsonPropertyName("externalReference")]
    public string ExternalReference { get; set; }

    [JsonPropertyName("visibleFrom")]
    public DateTimeOffset? VisibleFrom { get; set; }

    [JsonPropertyName("dueAt")]
    public DateTimeOffset? DueAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("systemLabel")]
    public string SystemLabel { get; set; } = "Default";

    [JsonPropertyName("content")]
    public Content Content { get; set; }

    [JsonPropertyName("searchTags")]
    public List<SearchTag> SearchTags { get; set; }

    [JsonPropertyName("attachments")]
    public List<Attachment> Attachments { get; set; }

    [JsonPropertyName("transmissions")]
    public List<Transmission> Transmissions { get; set; }

    [JsonPropertyName("guiActions")]
    public List<GuiAction> GuiActions { get; set; }

    [JsonPropertyName("apiActions")]
    public List<ApiAction> ApiActions { get; set; }

    [JsonPropertyName("activities")]
    public List<Activity> Activities { get; set; }
}
