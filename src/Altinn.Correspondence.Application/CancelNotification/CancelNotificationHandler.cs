using System.Runtime.CompilerServices;
using System.Threading;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;

[assembly: InternalsVisibleTo("Altinn.Correspondence.Tests")]
namespace Altinn.Correspondence.Application.CancelNotification
{
    public class CancelNotificationHandler
    {
        private readonly ILogger<CancelNotificationHandler> _logger;
        private readonly IAltinnNotificationService _altinnNotificationService;
        private readonly IDialogportenService _dialogportenService;
        private readonly ISlackClient _slackClient;
        private const string TestChannel = "#test-varslinger";
        private const string RetryCountKey = "RetryCount";
        private const int MaxRetries = 10;
        public CancelNotificationHandler(
            ILogger<CancelNotificationHandler> logger,
            IAltinnNotificationService altinnNotificationService,
            IDialogportenService dialogportenService,
            ISlackClient slackClient)
        {
            _logger = logger;
            _altinnNotificationService = altinnNotificationService;
            _dialogportenService = dialogportenService;
            _slackClient = slackClient;
        }

        [AutomaticRetry(Attempts = MaxRetries)]
        public async Task Process(PerformContext context, List<CorrespondenceNotificationEntity> notificationEntities, CancellationToken cancellationToken = default)
        {
            var retryAttempts = context.GetJobParameter<int>(RetryCountKey);
            _logger.LogInformation("Cancelling notifications for purged correspondence. Retry attempt: {retryAttempts}", retryAttempts);
            await CancelNotification(notificationEntities, retryAttempts, cancellationToken);
        }
        internal async Task CancelNotification(List<CorrespondenceNotificationEntity> notificationEntities, int retryAttempts, CancellationToken cancellationToken)
        {
            foreach (var notification in notificationEntities)
            {
                if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent

                string? notificationOrderId = notification.NotificationOrderId?.ToString();

                if (string.IsNullOrWhiteSpace(notificationOrderId))
                    {
                        var error = $"Error while cancelling notification. NotificationOrderId is null for notificationId: {notification.Id}";
                        if (retryAttempts == MaxRetries) SendSlackNotificationWithMessage(error);
                        throw new Exception(error);
                    }
                bool isCancellationSuccessful = await _altinnNotificationService.CancelNotification(notificationOrderId, cancellationToken);
                if (!isCancellationSuccessful)
                {
                    var error = $"Error while cancelling notification. Failed to cancel notification for notificationId: {notification.Id}";
                    if (retryAttempts == MaxRetries) SendSlackNotificationWithMessage(error);
                    throw new Exception(error);
                } else
                {
                    await _dialogportenService.CreateInformationActivity(notification.CorrespondenceId, DialogportenActorType.ServiceOwner, "Varslingsordre kansellert", cancellationToken: cancellationToken);
                }
            }
        }
        private void SendSlackNotificationWithMessage(string message)
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
