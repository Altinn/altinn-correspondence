using System.Net.Http.Json;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Exceptions;

namespace Altinn.Correspondence.Integrations.Altinn.Notifications;

public class AltinnNotificationService : IAltinnNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnNotificationService> _logger;

    public AltinnNotificationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Guid?> CreateNotification(NotificationOrderRequest notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating notification in Altinn Notification");
        var response = await _httpClient.PostAsJsonAsync("notifications/api/v1/orders", notification, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create notification in Altinn Notification. Status code: {StatusCode}", response.StatusCode);
            _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
            throw new NotificationCreationException("Failed to create notification in Altinn Notification");
        }
        var responseContent = await response.Content.ReadFromJsonAsync<NotificationOrderRequestResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Notification.");
            throw new NotificationCreationException("Failed to process response from Altinn Notification");
        }
        if (responseContent.RecipientLookup!.Status != RecipientLookupStatus.Success)
        {
            _logger.LogError(responseContent.RecipientLookup.Status.ToString());
            _logger.LogError("Recipient lookup failed when ordering notification.");
            throw new RecipientLookupException("Recipient lookup failed when ordering notification");
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

    public async Task<NotificationStatusResponse> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default)
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
        var notificationStatusResponse = new NotificationStatusResponse
        {
            Created = notificationSummary.Created,
            Creator = notificationSummary.Creator,
            Id = notificationSummary.Id,
            IgnoreReservation = notificationSummary.IgnoreReservation,
            NotificationChannel = notificationSummary.NotificationChannel,
            NotificationsStatusDetails = new NotificationsStatusDetails
            {

            },
            ProcessingStatus = notificationSummary.ProcessingStatus,
            RequestedSendTime = notificationSummary.RequestedSendTime,
            ResourceId = notificationSummary.ResourceId,
            SendersReference = notificationSummary.SendersReference
        };

        if (notificationSummary.NotificationsStatusSummary?.Email != null)
        {
            var emailResponse = await _httpClient.GetAsync(notificationSummary.NotificationsStatusSummary.Email.Links.Self, cancellationToken: cancellationToken);
            if (!emailResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get details about email notification from Altinn Notification. Status code: {StatusCode}", response.StatusCode);
                _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
                throw new BadHttpRequestException("Failed to process response from Altinn Notification");
            }
            var data = await emailResponse.Content.ReadFromJsonAsync<EmailNotificationSummary>(cancellationToken);
            if (data?.Notifications.Count > 0) notificationStatusResponse.NotificationsStatusDetails.Email = data?.Notifications[0];
        }
        else if (notificationSummary.NotificationsStatusSummary?.Sms != null)
        {
            var smsResponse = await _httpClient.GetAsync(notificationSummary.NotificationsStatusSummary.Sms.Links.Self, cancellationToken: cancellationToken);
            if (!smsResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get details about sms notification from Altinn Notification. Status code: {StatusCode}", response.StatusCode);
                _logger.LogError("Body: {Response}", await response.Content.ReadAsStringAsync(cancellationToken));
                throw new BadHttpRequestException("Failed to process response from Altinn Notification");
            }
            var data = await smsResponse.Content.ReadFromJsonAsync<SmsNotificationSummary>(cancellationToken);
            if (data?.Notifications.Count > 0) notificationStatusResponse.NotificationsStatusDetails.Sms = data.Notifications[0];
        }
        return notificationStatusResponse;
    }
}
