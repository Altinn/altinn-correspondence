using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Register;

/// <summary>
/// JSON envelope used by the Altinn Register query endpoints for both request and response bodies
/// in the form <c>{"data": [...]}</c>.
/// </summary>
/// <typeparam name="T">The type of items in the list</typeparam>
public class AltinnRegisterQueryData<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();
}
