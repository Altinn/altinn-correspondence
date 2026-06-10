using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.Monitor.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Altinn.Correspondence.Integrations.Azure;
public class AzureResourceManagerService : IResourceManager
{
    private const string FinopsProduct = "melding";
    private const string RepositoryUrl = "https://github.com/Altinn/altinn-correspondence";
    private const string DefenderForStorageDataScannerRoleDefinitionId = "1e7ca9b1-60d1-4db8-a914-f2ca1ff27c40";
    private const string EventGridDataSenderRoleDefinitionId = "d5a91429-5739-47e2-a06b-3470a27159e7";
    private const string DefenderForStorageSettingsApiVersion = "2025-06-01";
    private static readonly TimeSpan DefenderSetupPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DefenderSetupPollTimeout = TimeSpan.FromMinutes(5);

    private readonly AzureResourceManagerOptions _resourceManagerOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ArmClient _armClient;
    private readonly TokenCredential _credentials;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<AzureResourceManagerService> _logger;
    private string GetResourceGroupName(ServiceOwnerEntity serviceOwner) => $"serviceowner-{_resourceManagerOptions.Environment}-{serviceOwner.Name}-rg";

    private SubscriptionResource GetSubscription() => _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}"));

    private Dictionary<string, string> GetServiceOwnerResourceGroupTags(ServiceOwnerEntity serviceOwnerEntity)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customer_id"] = serviceOwnerEntity.Id,
            ["env"] = _resourceManagerOptions.Environment,
            ["finops_environment"] = _resourceManagerOptions.Environment,
            ["finops_product"] = FinopsProduct,
            ["finops_serviceownercode"] = serviceOwnerEntity.Name,
            ["finops_serviceownerorgnr"] = serviceOwnerEntity.Id,
            ["org"] = serviceOwnerEntity.Name,
            ["product"] = FinopsProduct,
            ["repository"] = RepositoryUrl
        };
    }

    public AzureResourceManagerService(
        IOptions<AzureResourceManagerOptions> resourceManagerOptions,
        IServiceOwnerRepository serviceOwnerRepository,
        IHostEnvironment hostingEnvironment,
        IBackgroundJobClient backgroundJobClient,
        ILogger<AzureResourceManagerService> logger)
    {
        _resourceManagerOptions = resourceManagerOptions.Value;
        _hostEnvironment = hostingEnvironment;
        _credentials = new DefaultAzureCredential();
        _armClient = new ArmClient(_credentials);
        _serviceOwnerRepository = serviceOwnerRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public void DeployStorageAccountsForServiceOwner(ServiceOwnerEntity serviceOwnerEntity, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating storage providers for {ServiceOwnerName}", serviceOwnerEntity.Name);
        var virusScanStorageProviderJob = _backgroundJobClient.Enqueue<IResourceManager>(service => service.DeployStorageAccount(serviceOwnerEntity, true, cancellationToken));
        _backgroundJobClient.ContinueJobWith<IResourceManager>(virusScanStorageProviderJob, service => service.DeployStorageAccount(serviceOwnerEntity, false, cancellationToken));
    }

    public async Task DeployStorageAccount(ServiceOwnerEntity serviceOwnerEntity, bool virusScan, CancellationToken cancellationToken)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            _logger.LogInformation("Development environment detected. Skipping deployment.");
            return;
        }
        _logger.LogInformation($"Starting deployment for {serviceOwnerEntity.Name}");
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity);

        var storageAccountName = GenerateStorageAccountName();
        _logger.LogInformation($"Resource group: {resourceGroupName}");
        _logger.LogInformation($"Storage account: {storageAccountName}");

        // Create or get the resource group
        var subscription = GetSubscription();
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupData = new ResourceGroupData(_resourceManagerOptions.Location);
        foreach (var tag in GetServiceOwnerResourceGroupTags(serviceOwnerEntity))
        {
            resourceGroupData.Tags[tag.Key] = tag.Value;
        }
        var resourceGroup = await resourceGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData, cancellationToken);

        // Create or get the storage account
        var storageAccountData = new StorageAccountCreateOrUpdateContent(new StorageSku(StorageSkuName.StandardZrs), StorageKind.StorageV2, new AzureLocation(_resourceManagerOptions.Location));
        storageAccountData.MinimumTlsVersion = "TLS1_2";
        storageAccountData.AllowSharedKeyAccess = false;
        foreach (var tag in GetServiceOwnerResourceGroupTags(serviceOwnerEntity))
        {
            storageAccountData.Tags[tag.Key] = tag.Value;
        }
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccount = await storageAccountCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageAccountData, cancellationToken);
        var blobService = storageAccount.Value.GetBlobService();
        string containerName = "attachments";
        if (!blobService.GetBlobContainers().Any(container => container.Data.Name == containerName))
        {
            await blobService.GetBlobContainers().CreateOrUpdateAsync(WaitUntil.Completed, containerName, new BlobContainerData(), cancellationToken);
        }
        if (virusScan)
        {
            await EnableMicrosoftDefender(resourceGroupName, storageAccountName, cancellationToken);
        }

        await ConfigureBlobDiagnosticSettings(blobService, cancellationToken);

        await _serviceOwnerRepository.InitializeStorageProvider(serviceOwnerEntity.Id, storageAccountName, virusScan ? StorageProviderType.Altinn3Azure : StorageProviderType.Altinn3AzureWithoutVirusScan);
        _logger.LogInformation($"Storage account {storageAccountName} created");
    }

    private async Task EnableMicrosoftDefender(string resourceGroupName, string storageAccountName, CancellationToken cancellationToken)
    {
        using var client = await CreateManagementHttpClientAsync(cancellationToken);
        var storageAccountResourceId = $"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}";
        await EnsureDefenderStorageScannerRoleAssignmentsAsync(client, storageAccountResourceId, cancellationToken);

        var defenderSettingsEndpoint = $"https://management.azure.com{storageAccountResourceId}/providers/Microsoft.Security/defenderForStorageSettings/current?api-version={DefenderForStorageSettingsApiVersion}";
        var requestBody = CreateMalwareScanConfiguration();
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PutAsync(defenderSettingsEndpoint, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to enable Defender malware scan for storage account {StorageAccountName} on attempt {Attempt}. Error: {Error}",
                    storageAccountName,
                    attempt,
                    responseBody);
                if (attempt == maxAttempts)
                {
                    throw new HttpRequestException($"Failed to enable Defender Malware Scan. Error: {responseBody}");
                }

                await Task.Delay(DefenderSetupPollInterval, cancellationToken);
                continue;
            }

            _logger.LogInformation(
                "Defender malware scan settings PUT returned {StatusCode} for storage account {StorageAccountName} on attempt {Attempt}. Response: {Response}",
                (int)response.StatusCode,
                storageAccountName,
                attempt,
                responseBody);

            var readyResponseBody = await WaitForMalwareScanningToBecomeOperationalAsync(
                client,
                defenderSettingsEndpoint,
                storageAccountResourceId,
                resourceGroupName,
                cancellationToken);
            if (readyResponseBody is not null)
            {
                _logger.LogInformation(
                    "Microsoft Defender on-upload malware scan is operational for storage account {StorageAccountName}: {Response}",
                    storageAccountName,
                    readyResponseBody);
                return;
            }

            _logger.LogWarning(
                "Defender malware scan was not operational for storage account {StorageAccountName} after attempt {Attempt}",
                storageAccountName,
                attempt);
        }

        throw new HttpRequestException(
            $"Defender on-upload malware scan did not become operational for storage account {storageAccountName} within {DefenderSetupPollTimeout.TotalMinutes} minutes.");
    }

    private MalwareScanConfiguration CreateMalwareScanConfiguration()
    {
        return new MalwareScanConfiguration()
        {
            Properties = new Properties()
            {
                IsEnabled = true,
                DataScannerResourceId = $"/subscriptions/{_resourceManagerOptions.SubscriptionId}/providers/Microsoft.Security/datascanners/StorageDataScanner",
                MalwareScanning = new MalwareScanning()
                {
                    BlobScanResultsOptions = "blobIndexTags",
                    OnUpload = new OnUpload()
                    {
                        IsEnabled = true,
                        CapGBPerMonth = -1
                    },
                    ScanResultsEventGridTopicResourceId = $"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{_resourceManagerOptions.ApplicationResourceGroupName}/providers/Microsoft.EventGrid/topics/{_resourceManagerOptions.MalwareScanEventGridTopicName}"
                },
                OverrideSubscriptionLevelSettings = true,
                SensitiveDataDiscovery = new SensitiveDataDiscovery()
                {
                    IsEnabled = false
                }
            }
        };
    }

    private async Task<string?> WaitForMalwareScanningToBecomeOperationalAsync(
        HttpClient client,
        string defenderSettingsEndpoint,
        string storageAccountResourceId,
        string resourceGroupName,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(DefenderSetupPollTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(defenderSettingsEndpoint, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to read Defender settings while waiting for malware scan setup. Status: {StatusCode}. Response: {Response}",
                    (int)response.StatusCode,
                    responseBody);
            }
            else if (IsMalwareScanningOperational(responseBody)
                && await HasStorageMalwareScanSystemTopicAsync(client, resourceGroupName, storageAccountResourceId, cancellationToken))
            {
                return responseBody;
            }
            else
            {
                _logger.LogInformation(
                    "Defender malware scan setup is still in progress for storage account {StorageAccountName}. Current response: {Response}",
                    storageAccountResourceId.Split('/', StringSplitOptions.RemoveEmptyEntries).Last(),
                    responseBody);
            }

            await Task.Delay(DefenderSetupPollInterval, cancellationToken);
        }

        return null;
    }

    private async Task<bool> HasStorageMalwareScanSystemTopicAsync(
        HttpClient client,
        string resourceGroupName,
        string storageAccountResourceId,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://management.azure.com/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.EventGrid/systemTopics?api-version=2022-06-15";
        using var response = await client.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Failed to list Event Grid system topics in resource group {ResourceGroupName}. Status: {StatusCode}. Error: {Error}",
                resourceGroupName,
                (int)response.StatusCode,
                error);
            return false;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("value", out var systemTopics))
        {
            return false;
        }

        foreach (var systemTopic in systemTopics.EnumerateArray())
        {
            if (!systemTopic.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            if (properties.TryGetProperty("source", out var source)
                && string.Equals(source.GetString(), storageAccountResourceId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    private async Task<HttpClient> CreateManagementHttpClientAsync(CancellationToken cancellationToken)
    {
        var client = new HttpClient();
        var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var token = await _credentials.GetTokenAsync(tokenRequestContext, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    private async Task EnsureDefenderStorageScannerRoleAssignmentsAsync(
        HttpClient client,
        string storageAccountResourceId,
        CancellationToken cancellationToken)
    {
        var storageDataScannerPrincipalId = await GetStorageDataScannerPrincipalIdAsync(client, cancellationToken);
        if (string.IsNullOrWhiteSpace(storageDataScannerPrincipalId))
        {
            _logger.LogWarning("StorageDataScanner principal id was not found. Skipping pre-provisioning of Defender role assignments.");
            return;
        }

        // Defender for Storage Scanner Operator only permits assigning these two roles (ABAC).
        // Storage Blob Data Owner is assigned automatically by the Defender platform — not by us.
        await EnsureRoleAssignmentAsync(
            client,
            storageAccountResourceId,
            DefenderForStorageDataScannerRoleDefinitionId,
            storageDataScannerPrincipalId,
            cancellationToken);
        await EnsureRoleAssignmentAsync(
            client,
            storageAccountResourceId,
            EventGridDataSenderRoleDefinitionId,
            storageDataScannerPrincipalId,
            cancellationToken);
    }

    private async Task<string?> GetStorageDataScannerPrincipalIdAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var endpoint = $"https://management.azure.com/subscriptions/{_resourceManagerOptions.SubscriptionId}/providers/Microsoft.Security/datascanners/StorageDataScanner?api-version=2024-07-01-preview";
        using var response = await client.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Failed to read StorageDataScanner resource. Status: {StatusCode}. Error: {Error}",
                (int)response.StatusCode,
                error);
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("identity", out var identity))
        {
            return null;
        }

        if (!identity.TryGetProperty("principalId", out var principalId))
        {
            return null;
        }

        return principalId.GetString();
    }

    private async Task EnsureRoleAssignmentAsync(
        HttpClient client,
        string scope,
        string roleDefinitionId,
        string principalId,
        CancellationToken cancellationToken)
    {
        var roleAssignmentName = CreateDeterministicGuid(scope, principalId, roleDefinitionId);
        var endpoint = $"https://management.azure.com/{scope.TrimStart('/')}/providers/Microsoft.Authorization/roleAssignments/{roleAssignmentName}?api-version=2022-04-01";
        var requestBody = new RoleAssignmentRequest
        {
            Properties = new RoleAssignmentProperties
            {
                RoleDefinitionId = $"/subscriptions/{_resourceManagerOptions.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{roleDefinitionId}",
                PrincipalId = principalId,
                PrincipalType = "ServicePrincipal"
            }
        };
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync(endpoint, content, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Ensured role assignment {RoleDefinitionId} for principal {PrincipalId} on scope {Scope}",
                roleDefinitionId,
                principalId,
                scope);
            return;
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation(
                "Role assignment {RoleDefinitionId} for principal {PrincipalId} on scope {Scope} already exists",
                roleDefinitionId,
                principalId,
                scope);
            return;
        }

        _logger.LogWarning(
            "Failed to ensure role assignment {RoleDefinitionId} for principal {PrincipalId} on scope {Scope}. Status: {StatusCode}. Error: {Error}",
            roleDefinitionId,
            principalId,
            scope,
            (int)response.StatusCode,
            error);
        throw new HttpRequestException(
            $"Failed to ensure role assignment {roleDefinitionId} on scope {scope}. Status: {(int)response.StatusCode}. Error: {error}");
    }

    private static string CreateDeterministicGuid(string scope, string principalId, string roleDefinitionId)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes($"{scope}|{principalId}|{roleDefinitionId}"));
        return new Guid(hash).ToString();
    }

    private static bool IsMalwareScanningOperational(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("properties", out var properties))
        {
            return false;
        }

        if (!properties.TryGetProperty("malwareScanning", out var malwareScanning))
        {
            return false;
        }

        if (!malwareScanning.TryGetProperty("onUpload", out var onUpload)
            || !onUpload.TryGetProperty("isEnabled", out var isEnabled)
            || !isEnabled.GetBoolean())
        {
            return false;
        }

        if (!malwareScanning.TryGetProperty("operationStatus", out var operationStatus)
            || !operationStatus.TryGetProperty("code", out var operationStatusCode))
        {
            return true;
        }

        return string.Equals(operationStatusCode.GetString(), "Succeeded", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ConfigureBlobDiagnosticSettings(BlobServiceResource blobService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_resourceManagerOptions.LogAnalyticsWorkspaceId))
        {
            _logger.LogWarning("Log Analytics workspace not configured. Skipping blob diagnostic settings.");
            return;
        }

        // Diagnostic settings target the blob service sub-resource, not the storage account.
        var diagnosticCollection = _armClient.GetDiagnosticSettings(blobService.Id);

        var diagnosticData = new DiagnosticSettingData
        {
            WorkspaceId = new ResourceIdentifier(_resourceManagerOptions.LogAnalyticsWorkspaceId),
            LogAnalyticsDestinationType = "Dedicated",
        };

        diagnosticData.Logs.Add(new LogSettings(true) { Category = "StorageRead" });
        diagnosticData.Logs.Add(new LogSettings(true) { Category = "StorageWrite" });
        diagnosticData.Logs.Add(new LogSettings(true) { Category = "StorageDelete" });

        var response = await diagnosticCollection.CreateOrUpdateAsync(
            WaitUntil.Completed,
            "audit-logs",
            diagnosticData,
            cancellationToken);

        if (!response.GetRawResponse().IsError)
        {
            _logger.LogInformation(
                "Configured blob diagnostic settings for {BlobServiceId} to workspace {WorkspaceId}",
                blobService.Id,
                _resourceManagerOptions.LogAnalyticsWorkspaceId);
            return;
        }

        var error = response.GetRawResponse().Content.ToString();
        _logger.LogError(
            "Failed to configure blob diagnostic settings for {BlobServiceId}. Error: {Error}",
            blobService.Id,
            error);
        throw new RequestFailedException(response.GetRawResponse());
    }

    private string GenerateStorageAccountName()
    {
        Random random = new Random();
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var obfuscationString = new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        return "aicorr" + obfuscationString + "sa";
    }

    public async Task UpdateContainerAppIpRestrictionsAsync(Dictionary<string, string> newIps, CancellationToken cancellationToken)
    {
        try 
        {
            var containerAppResourceId = new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{_resourceManagerOptions.ApplicationResourceGroupName}/providers/Microsoft.App/containerapps/{_resourceManagerOptions.ContainerAppName}");
            var containerApp = await _armClient.GetContainerAppResource(containerAppResourceId).GetAsync(cancellationToken);

            var ipRestrictions = containerApp.Value.Data.Configuration.Ingress.IPSecurityRestrictions;

            ipRestrictions.Clear();

            foreach (var ip in newIps)
            {
                ipRestrictions.Add(new ContainerAppIPSecurityRestrictionRule(name: $"IP whitelist {ip.Value}", action: ContainerAppIPRuleAction.Allow, ipAddressRange: ip.Key));
            }

            _logger.LogInformation("Updating IP restrictions for container app");
            var response = await containerApp.Value.UpdateAsync(waitUntil: WaitUntil.Started, data: containerApp.Value.Data, cancellationToken: cancellationToken);

            if (response.GetRawResponse().Status != 200)
            {
                _logger.LogError("Failed to update IP restrictions for container app. Status code: {StatusCode}", response.GetRawResponse().Status);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while updating IP security restrictions for container app");
        }
    }

    public async Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken)
    {
        try
        {
            Response<ServiceTagsListResult> response = await GetSubscription().GetServiceTagAsync(_resourceManagerOptions.Location, cancellationToken);
            if (response.GetRawResponse().Status != 200)
            {
                _logger.LogError("Failed to retrieve Azure service tags in Azure Resource Manager Service. Status code: {StatusCode}", response.GetRawResponse().Status);
                return null;
            }
            return response.Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while retrieving Azure service tags");
            return null;
        }
    }

    public async Task<Dictionary<string, string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken)
    {
        string serviceTagId = "AzureEventGrid";
        var serviceTagsListResult = await RetrieveServiceTags(cancellationToken);
        var retrievedAddresses = serviceTagsListResult?.Values
            .Where(v => string.Equals(v.Id, serviceTagId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(v => v.Properties.AddressPrefixes)
            .Where(ip => !ip.Contains(':'))
            .ToList();
        if (retrievedAddresses == null || retrievedAddresses.Count == 0)
        {
            _logger.LogError($"No EventGrid IP addresses were retrieved. Service tag '{serviceTagId}' may not exist.");
            return new Dictionary<string, string>();
        }
        Dictionary<string, string> addresses = retrievedAddresses.ToDictionary(ip => ip, ip => $"{serviceTagId} IP");
        if (!string.IsNullOrWhiteSpace(_resourceManagerOptions.ApimIP)) 
        { 
            addresses.Add(_resourceManagerOptions.ApimIP, "Apim IP");
        }
        return addresses;
    }
}
