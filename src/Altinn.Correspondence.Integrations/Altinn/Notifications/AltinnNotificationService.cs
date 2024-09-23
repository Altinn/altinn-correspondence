using System.Net.Http.Json;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Integrations.Altinn.Notifications;

public class AltinnNotificationService : IAltinnNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnNotificationService> _logger;

    public AltinnNotificationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnNotificationService> logger)
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
            _logger.LogError("Failed to create notification in Altinn Notification. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
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

    public async Task<NotificationOrderWithStatus> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"notifications/api/v1/orders/{orderId}/status", cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get resource from Altinn Notification. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
            throw new BadHttpRequestException("Failed to get notification details from Altinn Notifications");
        }

        var notificationSummary = await response.Content.ReadFromJsonAsync<NotificationOrderWithStatus>(cancellationToken: cancellationToken);
        if (notificationSummary is null)
        {
            _logger.LogError("Failed to deserialize response from Altinn Notification");
            throw new BadHttpRequestException("Failed to process response from Altinn Notification");
        }
        return notificationSummary;

    }
}
