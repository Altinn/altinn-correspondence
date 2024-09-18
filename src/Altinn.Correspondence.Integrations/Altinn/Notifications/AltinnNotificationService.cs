using System.Net.Http.Json;
using Altinn.Correspondence.Repositories;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace Altinn.Correspondence.Integrations.Altinn.Notifications;

public class AltinnNotificationService : IAltinnNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnNotificationService> _logger;

    public AltinnNotificationService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnNotificationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Guid?> CreateNotification(NotificationOrderRequest notification, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("notifications/api/v1/orders", notification, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(response.StatusCode.ToString());
            _logger.LogError(response.Content.Headers.ToString());
            return null;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<NotificationOrderRequestResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Notification.");
            return null;
        }
        if (responseContent.RecipientLookup!.Status != RecipientLookupStatus.Success)
        {
            _logger.LogError("Recipient lookup failed when ordering notification.");
            return null;
        }
        Console.WriteLine(responseContent.OrderId);
        return responseContent.OrderId;
    }

    public async Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsync($"notifications/api/v1/orders/{orderId}/cancel", null, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Could not cancel notification with orderId: " + orderId);
            return false;
        }
        return true;
    }
}
