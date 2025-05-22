using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models.Migration;

/// <summary>
/// Represents a container object for attachments used when initiating a shared attachment
/// </summary>
public class MigrateInitializeAttachmentExt : InitializeAttachmentExt
{
    [JsonPropertyName("senderPartyUuid")]
    public required Guid SenderPartyUuid { get; set; }

    [JsonPropertyName("altinn2AttachmentId")]
    public required string Altinn2AttachmentId { get; set; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; set; }

    /// <summary>
    /// A reference value given to the attachment by the creator.
    /// </summary>
    [JsonPropertyName("altinn2sendersReference")]
    public string? Altinn2SendersReference { get; set; } = string.Empty;
}
