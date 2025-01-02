using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry;

public class ContactReservationPersonRequest
{
    [JsonPropertyName("personidentifikatorer")]
    public List<string> Personidentifikatorer { get; set; }
}
