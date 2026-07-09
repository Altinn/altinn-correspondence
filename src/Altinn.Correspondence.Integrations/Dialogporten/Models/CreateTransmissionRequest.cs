using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

public class CreateTransmissionRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string? AuthorizationAttribute { get; set; }

    [JsonPropertyName("isAuthorized")]
    public bool IsAuthorized { get; set; }

    [JsonPropertyName("extendedType")]
    public string? ExtendedType { get; set; }

    [JsonPropertyName("externalReference")]
    public string? ExternalReference { get; set; }

    [JsonPropertyName("relatedTransmissionId")]
    public Guid? RelatedTransmissionId { get; set; }

    [JsonPropertyName("type")]
    public TransmissionType Type { get; set; }

    [JsonPropertyName("sender")]
    public required TransmissionSender Sender { get; set; }

    [JsonPropertyName("content")]
    public required TransmissionContent Content { get; set; }

    [JsonPropertyName("attachments")]
    public required List<TransmissionAttachment> Attachments { get; set; }
    }

public class TransmissionAttachment
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("displayName")]
    public required List<TransmissionDisplayName> DisplayName { get; set; }

    [JsonPropertyName("urls")]
    public required List<TransmissionUrl> Urls { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }
    }

    public class TransmissionContent
    {
        [JsonPropertyName("title")]
        public required TransmissionTitle Title { get; set; }

        [JsonPropertyName("summary")]
        public TransmissionSummary? Summary { get; set; }

        [JsonPropertyName("contentReference")]
        public required TransmissionContentReference ContentReference { get; set; }
    }

    public class TransmissionContentReference
    {
        [JsonPropertyName("value")]
        public required List<TransmissionValue> Value { get; set; }

        [JsonPropertyName("mediaType")]
        public required string MediaType { get; set; }
    }

    public class TransmissionDisplayName
    {
        [JsonPropertyName("value")]
        public required string Value { get; set; }

        [JsonPropertyName("languageCode")]
        public required string LanguageCode { get; set; }
    }

    public class TransmissionSender
    {
        [JsonPropertyName("actorType")]
        public required string ActorType { get; set; }

        [JsonPropertyName("actorName")]
        public string? ActorName { get; set; }

        [JsonPropertyName("actorId")]
        public string? ActorId { get; set; }
    }

    public class TransmissionSummary
    {
        [JsonPropertyName("value")]
        public required List<TransmissionValue> Value { get; set; }

        [JsonPropertyName("mediaType")]
        public required string MediaType { get; set; }
    }

    public class TransmissionTitle
    {
        [JsonPropertyName("value")]
        public required List<TransmissionValue> Value { get; set; }

        [JsonPropertyName("mediaType")]
        public required string MediaType { get; set; }
    }

    public class TransmissionUrl
    {
        [JsonPropertyName("url")]
        public required string Url { get; set; }

        [JsonPropertyName("mediaType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MediaType { get; set; }

        [JsonPropertyName("consumerType")]
        public required string ConsumerType { get; set; }
    }

    public class TransmissionValue
    {
        [JsonPropertyName("value")]
        public required string Value { get; set; }

        [JsonPropertyName("languageCode")]
        public required string LanguageCode { get; set; }
    }

    public enum TransmissionType
    {
        Information = 1,
        Acceptance = 2,
        Rejection = 3,
        Request = 4,
        Alert = 5,
        Decision = 6,
        Submission = 7,
        Correction = 8
    }