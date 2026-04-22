using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Services;

public class MaskinportenJwkKey
{
    [JsonPropertyName("kty")]
    public string Kty { get; set; } = string.Empty;

    [JsonPropertyName("kid")]
    public string Kid { get; set; } = string.Empty;

    [JsonPropertyName("use")]
    public string Use { get; set; } = string.Empty;

    [JsonPropertyName("alg")]
    public string Alg { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string N { get; set; } = string.Empty;

    [JsonPropertyName("e")]
    public string E { get; set; } = string.Empty;
}

public class MaskinportenJwkSet
{
    [JsonPropertyName("keys")]
    public List<MaskinportenJwkKey> Keys { get; set; } = [];
}

public class MaskinportenGeneratedJwk
{
    public string Kid { get; init; } = string.Empty;

    public string PrivateJwkJson { get; init; } = string.Empty;

    public string PrivateJwkBase64 { get; init; } = string.Empty;

    public MaskinportenJwkKey PublicJwk { get; init; } = new();
}

public class MaskinportenJwkRotationResult
{
    public string TargetClientId { get; init; } = string.Empty;

    public string TargetClientName { get; init; } = string.Empty;

    public string NewKid { get; init; } = string.Empty;

    public int PreviousKeyCount { get; init; }

    public int CurrentKeyCount { get; init; }

    public string VerificationScope { get; init; } = string.Empty;

    public string KeyVaultSecretName { get; init; } = string.Empty;
}
