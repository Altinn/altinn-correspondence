using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

public class MakeCorrespondenceAvailableRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public Guid? CorrespondenceId { get; set; }
    
    [JsonPropertyName("createEvents")]
    public bool CreateEvents { get; set; } = false;

    [JsonPropertyName("correspondenceIds")]
    public List<Guid>? CorrespondenceIds { get; set; }

    [JsonPropertyName("asyncProcessing")]
    public bool AsyncProcessing { get; set; } = false;

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; set; }
}
