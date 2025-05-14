using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Represents a container object for attachments used when initiating a shared attachment
/// </summary>
public class MigrateInitializeAttachmentExt : InitializeAttachmentExt
{
    [JsonPropertyName("senderPartyUuid")]
    public required Guid SenderPartyUuid { get; set; }

    [JsonPropertyName("altinn2AttachmentId")]
    public int? Altinn2AttachmentId { get; set; }
}
