using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry;

public class ContactReservationContactInformation
{
    [JsonPropertyName("epostadresse")]
    public string Epostadresse { get; set; }

    [JsonPropertyName("epostadresse_oppdatert")]
    public DateTime EpostadresseOppdatert { get; set; }

    [JsonPropertyName("epostadresse_sist_verifisert")]
    public DateTime EpostadresseSistVerifisert { get; set; }

    [JsonPropertyName("epostadresse_duplisert")]
    public string EpostadresseDuplisert { get; set; }

    [JsonPropertyName("mobiltelefonnummer")]
    public string Mobiltelefonnummer { get; set; }

    [JsonPropertyName("mobiltelefonnummer_oppdatert")]
    public DateTime MobiltelefonnummerOppdatert { get; set; }

    [JsonPropertyName("mobiltelefonnummer_sist_verifisert")]
    public DateTime MobiltelefonnummerSistVerifisert { get; set; }

    [JsonPropertyName("mobiltelefonnummer_duplisert")]
    public string MobiltelefonnummerDuplisert { get; set; }
}

public class ContactReservationPerson
{
    [JsonPropertyName("personidentifikator")]
    public string Personidentifikator { get; set; }

    [JsonPropertyName("reservasjon")]
    public string Reservasjon { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("varslingsstatus")]
    public string Varslingsstatus { get; set; }

    [JsonPropertyName("kontaktinformasjon")]
    public ContactReservationContactInformation Kontaktinformasjon { get; set; }

    [JsonPropertyName("oppdatert")]
    public DateTime Oppdatert { get; set; }
}

public class ContactReservationPersonResponse
{
    [JsonPropertyName("personer")]
    public List<ContactReservationPerson> Personer { get; set; }
}