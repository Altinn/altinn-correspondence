using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// Response object for initializing correspondences. Contains information about the created correspondences and their attachments.
/// </summary>
public class InitializeCorrespondencesResponseExt
{
    /// <summary>
    /// The initialized correspondences
    /// </summary>
    [JsonPropertyName("correspondences")]
    public List<InitializedCorrespondencesExt> Correspondences { get; set; }

    /// <summary>
    /// The IDs of the attachments that were included with the correspondences
    /// </summary>
    [JsonPropertyName("attachmentIds")]
    public List<Guid> AttachmentIds { get; set; }
}
