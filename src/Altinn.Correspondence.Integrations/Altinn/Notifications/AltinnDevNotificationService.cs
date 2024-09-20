using System.Net.Http.Json;
using Altinn.Correspondence.Repositories;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Integrations.Altinn.Notifications;

public class AltinnDevNotificationService : IAltinnNotificationService
{
    private readonly ILogger<AltinnNotificationService> _logger;

    public AltinnDevNotificationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnNotificationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _logger = logger;
    }

    public async Task<Guid?> CreateNotification(NotificationOrderRequest notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Notification for Correspondence with recipient ssn: " + notification.Recipients[0].NationalIdentityNumber + " or orgNr: " + notification.Recipients[0].OrganizationNumber);
        return Guid.NewGuid();
    }

    public async Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default)
    {
        return true;
    }

    public async Task<NotificationOrderWithStatus> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default)
    {
        return new NotificationOrderWithStatus
        {
            Created = DateTime.UtcNow,
            Creator = "Altinn",
            Id = orderId,
            NotificationChannel = NotificationChannel.Email,
            NotificationsStatusSummary = new NotificationsStatusSummary()
            {
                Email = new EmailNotificationStatusExt()
                {
                    Generated = 1,
                    Succeeded = 1,
                }
            }

        };
    }
}
