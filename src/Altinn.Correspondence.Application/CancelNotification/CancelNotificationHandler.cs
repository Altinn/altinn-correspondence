using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Altinn.Correspondence.Core.Options;

[assembly: InternalsVisibleTo("Altinn.Correspondence.Tests")]
namespace Altinn.Correspondence.Application.CancelNotification
{
    public class CancelNotificationHandler(
        ILogger<CancelNotificationHandler> logger,
        ICorrespondenceRepository correspondenceRepository,
        IBackgroundJobClient backgroundJobClient,
        IHostEnvironment hostEnvironment)
    {
        private const string RetryCountKey = "RetryCount";
        private const int MaxRetries = 10;

        [AutomaticRetry(Attempts = MaxRetries)]
        public async Task Process(PerformContext context, Guid correspondenceId, ClaimsPrincipal? _, CancellationToken cancellationToken = default)
        {
            var operationTimestamp = DateTimeOffset.UtcNow;

            var retryAttempts = context.GetJobParameter<int>(RetryCountKey);
            logger.LogInformation("Cancelling notifications for correspondence {correspondenceId}. Retry attempt: {retryAttempts}", correspondenceId, retryAttempts);
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
            var notificationEntities = correspondence?.Notifications ?? [];
            await CancelNotification(correspondenceId, notificationEntities, retryAttempts, operationTimestamp, cancellationToken);
        }
        public async Task CancelNotification(Guid correspondenceId, List<CorrespondenceNotificationEntity> notificationEntities, int retryAttempts, DateTimeOffset operationTimestamp, CancellationToken cancellationToken)
        {
            var env = hostEnvironment.EnvironmentName;
            var error = $"Error while attempting to cancel notifications for correspondenceId: {correspondenceId} in environment: {env}.";
            foreach (var notification in notificationEntities) //ok
            {
                if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent

                string? notificationOrderId = notification.NotificationOrderId?.ToString();
                if (string.IsNullOrWhiteSpace(notificationOrderId))
                {
                    error += $"NotificationOrderId is null for notificationId: {notification.Id}";
                    throw new Exception(error);
                }
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(notification.CorrespondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCancelled, operationTimestamp));
            }
        }
    }
}
