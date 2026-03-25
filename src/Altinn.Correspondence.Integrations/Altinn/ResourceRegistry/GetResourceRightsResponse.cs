using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

[assembly: InternalsVisibleTo("Altinn.Correspondence.Tests")]
namespace Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;

internal class GetResourceRightsResponse
{
    [JsonPropertyName("action")]
    public required RightAction Action { get; set; }

    [JsonPropertyName("resource")]
    public List<AttributeValue>? Resource { get; set; }

    [JsonPropertyName("subjects")]
    public List<Subject>? Subjects { get; set; }

    [JsonPropertyName("rightKey")]
    public string? RightKey { get; set; }

    [JsonPropertyName("subjectTypes")]
    public List<string>? SubjectTypes { get; set; }
}

internal class RightAction
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

internal class AttributeValue
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

internal class Subject
{
    [JsonPropertyName("subjectAttributes")]
    public List<AttributeValue>? SubjectAttributes { get; set; }
}