using System.Text.Json.Serialization;
public class CreateTransmissionRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("authorizationAttribute")]
    public string? AuthorizationAttribute { get; set; }

    [JsonPropertyName("isAuthorized")]
    public bool IsAuthorized { get; set; }

    [JsonPropertyName("extendedType")]
    public string? ExtendedType { get; set; }

    [JsonPropertyName("externalReference")]
    public string ExternalReference { get; set; }

    [JsonPropertyName("relatedTransmissionId")]
    public Guid? RelatedTransmissionId { get; set; }

    [JsonPropertyName("type")]
    public TransmissionType Type { get; set; }

    [JsonPropertyName("sender")]
    public TransmissionSender Sender { get; set; }

    [JsonPropertyName("content")]
    public TransmissionContent Content { get; set; }

    [JsonPropertyName("attachments")]
    public List<TransmissionAttachment> Attachments { get; set; }

    [JsonPropertyName("title")]
    public TransmissionTitle Title { get; set; }
    }

public class TransmissionAttachment
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("displayName")]
    public List<TransmissionDisplayName> DisplayName { get; set; }

    [JsonPropertyName("urls")]
    public List<TransmissionUrl> Urls { get; set; }
    }

    public class TransmissionContent
    {
        [JsonPropertyName("title")]
        public TransmissionTitle Title { get; set; }

        [JsonPropertyName("summary")]
        public TransmissionSummary Summary { get; set; }

        [JsonPropertyName("contentReference")]
        public TransmissionContentReference ContentReference { get; set; }
    }

    public class TransmissionContentReference
    {
        [JsonPropertyName("value")]
        public List<TransmissionValue> Value { get; set; }

        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }
    }

    public class TransmissionDisplayName
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; }
    }

    public class TransmissionSender
    {
        [JsonPropertyName("actorType")]
        public string ActorType { get; set; }

        [JsonPropertyName("actorName")]
        public string ActorName { get; set; }

        [JsonPropertyName("actorId")]
        public string ActorId { get; set; }
    }

    public class TransmissionSummary
    {
        [JsonPropertyName("value")]
        public List<TransmissionValue> Value { get; set; }

        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }
    }

    public class TransmissionTitle
    {
        [JsonPropertyName("value")]
        public List<TransmissionValue> Value { get; set; }

        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }
    }

    public class TransmissionUrl
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }

        [JsonPropertyName("consumerType")]
        public string ConsumerType { get; set; }
    }

    public class TransmissionValue
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; }
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