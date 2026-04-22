using Altinn.Correspondence.Core.Services;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Altinn.Correspondence.Integrations.Azure;

public class KeyVaultSecretStore : IKeyVaultSecretStore
{
    public async Task SetSecretAsync(string vaultUrl, string secretName, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vaultUrl))
        {
            throw new InvalidOperationException("Key Vault URL is missing for Maskinporten JWK rotation.");
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new InvalidOperationException("Key Vault secret name is missing for Maskinporten JWK rotation.");
        }

        var client = new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
        await client.SetSecretAsync(secretName, value, cancellationToken);
    }
}
