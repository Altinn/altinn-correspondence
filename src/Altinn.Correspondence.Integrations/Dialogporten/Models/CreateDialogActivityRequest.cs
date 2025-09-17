using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Dialogporten.Models;

public class ActivityDescription
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}

public class ActivityPerformedBy
{
    [JsonPropertyName("actorType")]
    public string ActorType { get; set; }

    [JsonPropertyName("actorName")]
    public string ActorName { get; set; }

    [JsonPropertyName("actorId")]
    public string ActorId { get; set; }
}

public enum ActivityType
{
    DialogCreated,
    DialogClosed,
    Information,
    TransmissionOpened,
    PaymentMade,
    SignatureProvided,
    CorrespondenceOpened,
    DialogDeleted,
    CorrespondenceConfirmed
}

public class CreateDialogActivityRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("extendedType")]
    public string ExtendedType { get; set; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActivityType Type { get; set; }

    [JsonPropertyName("relatedActivityId")]
    public string RelatedActivityId { get; set; }

    [JsonPropertyName("transmissionId")]
    public string TransmissionId { get; set; }

    [JsonPropertyName("performedBy")]
    public ActivityPerformedBy PerformedBy { get; set; }

    [JsonPropertyName("description")]
    public List<ActivityDescription> Description { get; set; }
}
