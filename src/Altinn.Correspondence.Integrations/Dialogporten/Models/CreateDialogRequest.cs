using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

//TODO: Dobbeltsjekk om disse er riktige!
public class CreateDialogRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("serviceResource")]
    public required string ServiceResource { get; set; }

    [JsonPropertyName("party")]
    public required string Party { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }

    [JsonPropertyName("process")]
    public string? Process { get; set; }

    [JsonPropertyName("extendedStatus")]
    public string? ExtendedStatus { get; set; }

    [JsonPropertyName("externalReference")]
    public required string ExternalReference { get; set; }

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

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("systemLabel")]
    public string SystemLabel { get; set; } = "Default";

    [JsonPropertyName("content")]
    public required Content Content { get; set; }

    [JsonPropertyName("searchTags")]
    public List<SearchTag>? SearchTags { get; set; }

    [JsonPropertyName("attachments")]
    public List<Attachment>? Attachments { get; set; }

    [JsonPropertyName("transmissions")]
    public List<Transmission>? Transmissions { get; set; }

    [JsonPropertyName("guiActions")]
    public required List<GuiAction> GuiActions { get; set; }

    [JsonPropertyName("apiActions")]
    public required List<ApiAction> ApiActions { get; set; }

    [JsonPropertyName("activities")]
    public List<Activity> Activities { get; set; } = new List<Activity>();

    [JsonPropertyName("serviceOwnerContext")]
    public ServiceOwnerContext? ServiceOwnerContext { get; set; }
}

public class Activity
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("extendedType")]
    public string? ExtendedType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("relatedActivityId")]
    public string? RelatedActivityId { get; set; }

    [JsonPropertyName("transmissionId")]
    public string? TransmissionId { get; set; }

    [JsonPropertyName("performedBy")]
    public required PerformedBy PerformedBy { get; set; }

    [JsonPropertyName("description")]
    public required List<Description> Description { get; set; }
}

public class ApiAction
{
    [JsonPropertyName("action")]
    public required string Action { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string? AuthorizationAttribute { get; set; }

    [JsonPropertyName("endpoints")]
    public required List<Endpoint> Endpoints { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class Attachment
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("displayName")]
    public required List<DisplayName> DisplayName { get; set; }

    [JsonPropertyName("urls")]
    public required List<DialogUrl> Urls { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class Content
{
    [JsonPropertyName("title")]
    public required ContentValue Title { get; set; }

    [JsonPropertyName("summary")]
    public ContentValue? Summary { get; set; }

    [JsonPropertyName("senderName")]
    public ContentValue? SenderName { get; set; }

    [JsonPropertyName("additionalInfo")]
    public ContentValue? AdditionalInfo { get; set; }

    [JsonPropertyName("extendedStatus")]
    public ExtendedStatus? ExtendedStatus { get; set; }

    // Used for embedded iframes
    [JsonPropertyName("mainContentReference")]
    public ContentValue? MainContentReference { get; set; }
}

public class Description
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public required string LanguageCode { get; set; }
}

public class DisplayName
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public required string LanguageCode { get; set; }
}

public class Endpoint
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("httpMethod")]
    public required string HttpMethod { get; set; }

    [JsonPropertyName("documentationUrl")]
    public string? DocumentationUrl { get; set; }

    [JsonPropertyName("requestSchema")]
    public string? RequestSchema { get; set; }

    [JsonPropertyName("responseSchema")]
    public string? ResponseSchema { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; } = false;

    [JsonPropertyName("sunsetAt")]
    public DateTimeOffset? SunsetAt { get; set; }
}

public class ExtendedStatus
{
    [JsonPropertyName("value")]
    public List<DialogValue>? Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }
}

public class GuiAction
{
    [JsonPropertyName("action")]
    public required string Action { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string? AuthorizationAttribute { get; set; }

    [JsonPropertyName("isDeleteDialogAction")]
    public bool IsDeleteDialogAction { get; set; }

    [JsonPropertyName("httpMethod")]
    public required string HttpMethod { get; set; }

    [JsonPropertyName("priority")]
    public required string Priority { get; set; }

    [JsonPropertyName("title")]
    public required List<Title> Title { get; set; }

    [JsonPropertyName("prompt")]
    public List<Prompt>? Prompt { get; set; }
}

public class PerformedBy
{
    [JsonPropertyName("actorType")]
    public required string ActorType { get; set; }

    [JsonPropertyName("actorName")]
    public string? ActorName { get; set; }

    [JsonPropertyName("actorId")]
    public string? ActorId { get; set; }
}

public class Prompt
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("languageCode")]
    public string? LanguageCode { get; set; }
}

public class SearchTag
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

public class Sender
{
    [JsonPropertyName("actorType")]
    public required string ActorType { get; set; }

    [JsonPropertyName("actorName")]
    public string? ActorName { get; set; }

    [JsonPropertyName("actorId")]
    public string? ActorId { get; set; }
}

public class SenderName
{
    [JsonPropertyName("value")]
    public required List<DialogValue> Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }
}

public class ContentValue
{
    [JsonPropertyName("value")]
    public required List<DialogValue> Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }
}

public class Title
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("languageCode")]
    public required string LanguageCode { get; set; }
}

public class Transmission
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string? AuthorizationAttribute { get; set; }

    [JsonPropertyName("extendedType")]
    public string? ExtendedType { get; set; }

    [JsonPropertyName("relatedTransmissionId")]
    public string? RelatedTransmissionId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sender")]
    public Sender? Sender { get; set; }

    [JsonPropertyName("content")]
    public Content? Content { get; set; }

    [JsonPropertyName("attachments")]
    public List<Attachment>? Attachments { get; set; }
}

public class DialogUrl
{
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    [JsonPropertyName("consumerType")]
    public string? ConsumerType { get; set; }
}

public class DialogValue
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public required string LanguageCode { get; set; }
}

public class ServiceOwnerContext
{
    [JsonPropertyName("serviceOwnerLabels")]
    public required List<ServiceOwnerLabel> ServiceOwnerLabels { get; set; }
}

public class ServiceOwnerLabel
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }
}