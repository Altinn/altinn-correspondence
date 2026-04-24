namespace Altinn.Correspondence.Core.Options;

public class MaskinportenJwkRotationSettings
{
    public bool Enabled { get; set; }

    public string CronExpression { get; set; } = "0 0 1 * *";

    public int VerificationMaxAttempts { get; set; } = 6;

    public int VerificationDelaySeconds { get; set; } = 15;

    public string AdminClientId { get; set; } = string.Empty;

    public string AdminEncodedJwk { get; set; } = string.Empty;

    public string AdminScope { get; set; } = "idporten:dcr.write";

    public string AdminApiBaseUrl { get; set; } = string.Empty;

    public string AdminKeyVaultSecretName { get; set; } = "maskinporten-admin-jwk";

    public string AdminClientIdKeyVaultSecretName { get; set; } = "maskinporten-admin-client-id";

    public string AdminNewKeyIdPrefix { get; set; } = "altinn-correspondence-maskinporten-admin";

    public string KeyVaultUrl { get; set; } = string.Empty;

    public string AdditionalKeyVaultUrls { get; set; } = string.Empty;

    public string KeyVaultSecretName { get; set; } = "maskinporten-jwk";

    public string TargetClientIdKeyVaultSecretName { get; set; } = "maskinporten-client-id";

    public string NewKeyIdPrefix { get; set; } = "altinn-correspondence-maskinporten";
}
