using System.Text.Json.Serialization;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.API.Models;

public class MakeCorrespondenceAvailableRequestExt
{
    [JsonPropertyName("correspondenceId")]
    public Guid? CorrespondenceId { get; set; }
    
    [JsonPropertyName("createEvents")]
    public bool CreateEvents { get; set; } = false;

    [JsonPropertyName("correspondenceIds")]
    public List<Guid>? CorrespondenceIds { get; set; }
}
