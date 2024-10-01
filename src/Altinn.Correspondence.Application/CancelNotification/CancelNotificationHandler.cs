using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;

namespace Altinn.Correspondence.Application.CancelNotification
{
    public class CancelNotificationHandler
    {
        private readonly ILogger<CancelNotificationHandler> _logger;
        private readonly IAltinnNotificationService _altinnNotificationService;
        private readonly ISlackClient _slackClient;
        private const string TestChannel = "#test-varslinger";
        private const int MaxRetries = 2;
        public CancelNotificationHandler(
            ILogger<CancelNotificationHandler> logger,
            IAltinnNotificationService altinnNotificationService,
            ISlackClient slackClient)
        {
            _logger = logger;
            _altinnNotificationService = altinnNotificationService;
            _slackClient = slackClient;
        }
        [AutomaticRetry(Attempts = MaxRetries, DelaysInSeconds = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1])]
        public async Task Process(PerformContext context, List<CorrespondenceNotificationEntity> notificationEntities, CancellationToken cancellationToken = default)
        {
            var retryAttempts = context.GetJobParameter<int>("RetryCount");
            _logger.LogInformation($"Cancelling notifications for purged correspondence. Retry attempt: {retryAttempts}");
            foreach (var notification in notificationEntities)
            {
                if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent
                notification.NotificationOrderId = null;

                if (notification.NotificationOrderId?.ToString() is not string notificationId)
                {
                    var error = $"Error while cancelling notification. NotificationOrderId is null for notificationId: {notification.Id}";
                    if (retryAttempts == MaxRetries)
                    {
                        SendSlackNotification(error);
                    }
                    throw new Exception(error);
                }
                bool isCancellationSuccessful = await _altinnNotificationService.CancelNotification(notificationId, cancellationToken);
                if (!isCancellationSuccessful)
                {
                    var error = $"Failed to cancel notification {notificationId}";
                    if (retryAttempts == MaxRetries)
                    {
                        SendSlackNotification(error);
                    }
                    throw new Exception(error);
                }
            }
        }
        private void SendSlackNotification(string message)
        {
            var slackMessage = new SlackMessage
            {
                Text = message,
                Channel = TestChannel,
            };
            _slackClient.Post(slackMessage);
        }
    }
}