using Altinn.Correspondence.Core.Models.Entities;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.Storage;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Altinn.Correspondence.Integrations.Azure
{
    public class SasTokenService
    {
        private readonly AzureResourceManagerOptions _resourceManagerOptions;
        private readonly ConcurrentDictionary<string, (DateTime Created, string Token)> _sasTokens =
            new ConcurrentDictionary<string, (DateTime Created, string Token)>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ArmClient _armClient;
        private readonly TokenCredential _credentials;
        private readonly ILogger<SasTokenService> _logger;

        public SasTokenService(IOptions<AzureResourceManagerOptions> options, ILogger<SasTokenService> logger)
        {
            _credentials = new DefaultAzureCredential();
            _armClient = new ArmClient(_credentials);
            _armClient = new ArmClient(new DefaultAzureCredential());
            _resourceManagerOptions = options.Value;
            _logger = logger;
        }

        private string GetResourceGroupName(string serviceOwnerId) => $"serviceowner-{_resourceManagerOptions.Environment}-{serviceOwnerId.Replace(":", "-")}-rg";
        private SubscriptionResource GetSubscription() => _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}"));

        public async Task<string> GetSasToken(StorageProviderEntity storageProviderEntity, string storageAccountName)
        {
            if (_sasTokens.TryGetValue(storageAccountName, out (DateTime Created, string Token) sasToken) && sasToken.Created.AddHours(8) > DateTime.UtcNow)
            {
                _logger.LogInformation($"Got sas token from cache.");
                return sasToken.Token;
            }

            _sasTokens.TryRemove(storageAccountName, out _);

            await _semaphore.WaitAsync();
            try
            {
                if (_sasTokens.TryGetValue(storageAccountName, out sasToken))
                {
                    return sasToken.Token;
                }
                (DateTime Created, string Token) newSasToken = default;
                newSasToken.Created = DateTime.UtcNow;
                newSasToken.Token = await CreateSasToken(storageProviderEntity, storageAccountName);

                _sasTokens.TryAdd(storageAccountName, newSasToken);

                return newSasToken.Token;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        private async Task<string> CreateSasToken(StorageProviderEntity storageProviderEntity, string storageAccountName)
        {
            _logger.LogInformation($"Creating new SAS token for {storageProviderEntity.ServiceOwnerId}: {storageProviderEntity.StorageResourceName}");
            var resourceGroupName = GetResourceGroupName(storageProviderEntity.ServiceOwnerId);
            var subscription = GetSubscription();
            var resourceGroupCollection = subscription.GetResourceGroups();
            var resourceGroup = await resourceGroupCollection.GetAsync(resourceGroupName);
            var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
            var storageAccount = await storageAccountCollection.GetAsync(storageAccountName);
            string accountKey = "";
            var keys = storageAccount.Value.GetKeysAsync();
            await using (var keyEnumerator = keys.GetAsyncEnumerator())
            {
                accountKey = await keyEnumerator.MoveNextAsync() ? keyEnumerator.Current.Value : "";
            }
            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(storageAccountName, accountKey);
            var containerName = "attachments";
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = containerName,
                Resource = "c",
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(24),
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Create | BlobSasPermissions.List | BlobSasPermissions.Write | BlobSasPermissions.Delete);
            string sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
            _logger.LogInformation("SAS Token created");
            return sasToken;
        }

    }
}
