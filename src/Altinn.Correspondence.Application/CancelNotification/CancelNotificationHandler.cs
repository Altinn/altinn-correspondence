using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CancelNotification
{
    public class CancelNotificationHandler
    {
        private readonly ILogger<CancelNotificationHandler> _logger;
        private readonly IAltinnNotificationService _altinnNotificationService;
        public CancelNotificationHandler(
            ILogger<CancelNotificationHandler> logger,
            IAltinnNotificationService altinnNotificationService
            )
        {
            _logger = logger;
            _altinnNotificationService = altinnNotificationService;
        }
        public async Task Process(PerformContext context, List<CorrespondenceNotificationEntity> notificationEntities, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cancelling notifications");
            var retryAttempts = context.GetJobParameter<int>("RetryCount");
            _logger.LogInformation($"Retry attempt: {retryAttempts}");
            if (retryAttempts == 10)
            {
                // TODO: Add Slack notification
            }
            foreach (var notification in notificationEntities)
            {
                if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent

                if (notification.NotificationOrderId?.ToString() is not string notificationId)
                {
                    throw new Exception("NotificationOrderId is null");
                }
                bool isCancellationSuccessful = await _altinnNotificationService.CancelNotification(notificationId, cancellationToken);
                if (!isCancellationSuccessful)
                {
                    throw new Exception("Failed to cancel notification");
                }
            }
        }
    }
}