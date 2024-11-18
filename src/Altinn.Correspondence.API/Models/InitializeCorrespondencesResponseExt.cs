using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondencesResponseExt
{
    [JsonPropertyName("correspondences")]
    public List<InitializedCorrespondencesExt> Correspondences { get; set; }
    [JsonPropertyName("attachmentIds")]
    public List<Guid> AttachmentIds { get; set; }
}


public class InitializedCorrespondencesExt
{
    [JsonPropertyName("correspondenceId")]
    public Guid CorrespondenceId { get; set; }

    [JsonPropertyName("status")]
    public CorrespondenceStatusExt Status { get; set; }

    [JsonPropertyName("recipient")]
    public required string Recipient { get; set; }

    [JsonPropertyName("notifications")]
    public List<InitializedCorrespondencesNotificationsExt>? Notifications { get; set; }
}
public class InitializedCorrespondencesNotificationsExt
{
    [JsonPropertyName("orderId")]
    public Guid? OrderId { get; set; }

    [JsonPropertyName("isReminder")]
    public bool? IsReminder { get; set; }

    [JsonPropertyName("status")]
    public InitializedNotificationStatusExt Status { get; set; }
}
public enum InitializedNotificationStatusExt
{
    Success,
    MissingContact,
    Failure,
}