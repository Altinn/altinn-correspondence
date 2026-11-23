using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;

internal class AccessListMembershipListResponse
{
    [JsonPropertyName("data")]
    public List<AccessListMembership>? Data { get; set; }
}

internal class AccessListMembership
{
    [JsonPropertyName("party")]
    public string? Party { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }
}
