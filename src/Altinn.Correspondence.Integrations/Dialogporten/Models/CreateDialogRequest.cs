using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

public class CreateDialogRequest
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
    public string? ExternalReference { get; set; }

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
    public string Status { get; set; }

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

    [JsonPropertyName("skipUnreadTrigger")]
    public bool SkipUnreadTrigger { get; set; }
}

public class Activity
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("extendedType")]
    public string ExtendedType { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("relatedActivityId")]
    public string RelatedActivityId { get; set; }

    [JsonPropertyName("transmissionId")]
    public string TransmissionId { get; set; }

    [JsonPropertyName("performedBy")]
    public PerformedBy PerformedBy { get; set; }

    [JsonPropertyName("description")]
    public List<Description> Description { get; set; }
}

public class ApiAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string AuthorizationAttribute { get; set; }

    [JsonPropertyName("endpoints")]
    public List<Endpoint> Endpoints { get; set; }
}

public class Attachment
{
    [JsonPropertyName("displayName")]
    public List<DisplayName> DisplayName { get; set; }

    [JsonPropertyName("urls")]
    public List<DialogUrl> Urls { get; set; }
}

public class Content
{
    [JsonPropertyName("title")]
    public ContentValue Title { get; set; }

    [JsonPropertyName("summary")]
    public ContentValue Summary { get; set; }

    [JsonPropertyName("senderName")]
    public ContentValue SenderName { get; set; }

    [JsonPropertyName("additionalInfo")]
    public ContentValue AdditionalInfo { get; set; }

    [JsonPropertyName("extendedStatus")]
    public ExtendedStatus ExtendedStatus { get; set; }

    // Used for embedded iframes
    [JsonPropertyName("mainContentReference")]
    public ContentValue? MainContentReference { get; set; }
}

public class Description
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}

public class DisplayName
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}

public class Endpoint
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("httpMethod")]
    public string HttpMethod { get; set; }

    [JsonPropertyName("documentationUrl")]
    public string DocumentationUrl { get; set; }

    [JsonPropertyName("requestSchema")]
    public string RequestSchema { get; set; }

    [JsonPropertyName("responseSchema")]
    public string ResponseSchema { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; } = false;

    [JsonPropertyName("sunsetAt")]
    public DateTimeOffset? SunsetAt { get; set; }
}

public class ExtendedStatus
{
    [JsonPropertyName("value")]
    public List<DialogValue> Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; }
}

public class GuiAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string AuthorizationAttribute { get; set; }

    [JsonPropertyName("isDeleteDialogAction")]
    public bool IsDeleteDialogAction { get; set; }

    [JsonPropertyName("httpMethod")]
    public string HttpMethod { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; }

    [JsonPropertyName("title")]
    public List<Title> Title { get; set; }

    [JsonPropertyName("prompt")]
    public List<Prompt> Prompt { get; set; }
}

public class PerformedBy
{
    [JsonPropertyName("actorType")]
    public string ActorType { get; set; }

    [JsonPropertyName("actorName")]
    public string ActorName { get; set; }

    [JsonPropertyName("actorId")]
    public string ActorId { get; set; }
}

public class Prompt
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}

public class SearchTag
{
    [JsonPropertyName("value")]
    public string Value { get; set; }
}

public class Sender
{
    [JsonPropertyName("actorType")]
    public string ActorType { get; set; }

    [JsonPropertyName("actorName")]
    public string ActorName { get; set; }

    [JsonPropertyName("actorId")]
    public string ActorId { get; set; }
}

public class SenderName
{
    [JsonPropertyName("value")]
    public List<DialogValue> Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; }
}

public class ContentValue
{
    [JsonPropertyName("value")]
    public List<DialogValue> Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; }
}

public class Title
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}

public class Transmission
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string AuthorizationAttribute { get; set; }

    [JsonPropertyName("extendedType")]
    public string ExtendedType { get; set; }

    [JsonPropertyName("relatedTransmissionId")]
    public string RelatedTransmissionId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("sender")]
    public Sender Sender { get; set; }

    [JsonPropertyName("content")]
    public Content Content { get; set; }

    [JsonPropertyName("attachments")]
    public List<Attachment> Attachments { get; set; }
}

public class DialogUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; }

    [JsonPropertyName("consumerType")]
    public string ConsumerType { get; set; }
}

public class DialogValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}
