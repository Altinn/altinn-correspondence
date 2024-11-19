using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;

[assembly: InternalsVisibleTo("Altinn.Correspondence.Tests")]
namespace Altinn.Correspondence.Application.CancelNotification
{
    public class CancelNotificationHandler
    {
        private readonly ILogger<CancelNotificationHandler> _logger;
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly IAltinnNotificationService _altinnNotificationService;
        private readonly ISlackClient _slackClient;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IHostEnvironment _hostEnvironment;
        private const string TestChannel = "#test-varslinger";
        private const string RetryCountKey = "RetryCount";
        private const int MaxRetries = 10;
        public CancelNotificationHandler(
            ILogger<CancelNotificationHandler> logger,
            ICorrespondenceRepository correspondenceRepository,
            IAltinnNotificationService altinnNotificationService,
            ISlackClient slackClient,
            IBackgroundJobClient backgroundJobClient,
            IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _correspondenceRepository = correspondenceRepository;
            _altinnNotificationService = altinnNotificationService;
            _slackClient = slackClient;
            _backgroundJobClient = backgroundJobClient;
            _hostEnvironment = hostEnvironment;
        }

        [AutomaticRetry(Attempts = MaxRetries)]
        public async Task Process(PerformContext context, Guid correspondenceId, ClaimsPrincipal? _, CancellationToken cancellationToken = default)
        {
            var retryAttempts = context.GetJobParameter<int>(RetryCountKey);
            _logger.LogInformation("Cancelling notifications for purged correspondence. Retry attempt: {retryAttempts}", retryAttempts);
            var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, cancellationToken);
            var notificationEntities = correspondence?.Notifications ?? [];
            await CancelNotification(correspondenceId, notificationEntities, retryAttempts, cancellationToken);
        }
        internal async Task CancelNotification(Guid correspondenceId, List<CorrespondenceNotificationEntity> notificationEntities, int retryAttempts, CancellationToken cancellationToken)
        {
            var env = _hostEnvironment.EnvironmentName;
            var error = $"Error while attempting to cancel notifications for correspondenceId: {correspondenceId} in environment: {env}.";
            foreach (var notification in notificationEntities)
            {
                if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent

                string? notificationOrderId = notification.NotificationOrderId?.ToString();

                if (string.IsNullOrWhiteSpace(notificationOrderId))
                {
                    error += $"NotificationOrderId is null for notificationId: {notification.Id}";
                    if (retryAttempts == MaxRetries) SendSlackNotificationWithMessage(error);
                    throw new Exception(error);
                }
                bool isCancellationSuccessful = await _altinnNotificationService.CancelNotification(notificationOrderId, cancellationToken);
                if (!isCancellationSuccessful)
                {
                    error += $"Cancellation unsuccessful for notificationId: {notification.Id}";
                    if (retryAttempts == MaxRetries) SendSlackNotificationWithMessage(error);
                    throw new Exception(error);
                }
                else
                {
                    _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(notification.CorrespondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCancelled));
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
