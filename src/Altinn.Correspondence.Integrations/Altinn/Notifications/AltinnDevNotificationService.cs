using Altinn.Correspondence.Core.Options;
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

    public async Task<NotificationOrderRequestResponseV2?> CreateNotificationV2(NotificationOrderRequestV2 notificationRequest, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Notification (versjon 2): ");
        return new NotificationOrderRequestResponseV2()
        {
            NotificationOrderId = Guid.NewGuid(),
            Notification = new NotificationResponseV2()
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "AltinnCorrespondence"
            }
        };
    }

    public async Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default)
    {
        return true;
    }

    public async Task<NotificationStatusResponse> GetNotificationDetails(string orderId, CancellationToken cancellationToken = default)
    {
        return new NotificationStatusResponse
        {
            Created = DateTime.UtcNow,
            Creator = "Altinn",
            Id = orderId,
            NotificationChannel = NotificationChannel.Email,
            IgnoreReservation = false,
            NotificationsStatusDetails = new NotificationsStatusDetails
            {
                Email = new EmailNotificationWithResult
                {
                    Id = new Guid(),
                    Recipient = new Recipient
                    {
                        EmailAddress = "test@test.no",
                    },
                    SendStatus = new StatusExt()
                    {
                        LastUpdate = DateTime.UtcNow,
                        Status = "Completed",
                        StatusDescription = "Notification processed successfully"
                    },
                    Succeeded = true
                }
            },
            ProcessingStatus = new StatusExt
            {
                LastUpdate = DateTime.UtcNow,
                Status = "Completed",
                StatusDescription = "Notification processed successfully"
            },
            RequestedSendTime = DateTime.UtcNow,
            SendersReference = "AltinnCorrespondence"
        };
    }

    public async Task<NotificationStatusResponseV2> GetNotificationDetailsV2(string shipmentId, CancellationToken cancellationToken = default)
    {
        return new NotificationStatusResponseV2()
        {
            ShipmentId = Guid.Parse(shipmentId),
            SendersReference = "AltinnCorrespondence",
            Type = "Email",
            Status = "Completed",
            Recipients = []
        };
    }
}
