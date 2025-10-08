using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Altinn.Correspondence.Tests")]
namespace Altinn.Correspondence.Application.CancelNotification
{
    public class CancelNotificationHandler(
        ILogger<CancelNotificationHandler> logger,
        ICorrespondenceRepository correspondenceRepository,
        IBackgroundJobClient backgroundJobClient)
    {
        public async Task Process(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            var operationTimestamp = DateTimeOffset.UtcNow;
            logger.LogInformation("Sending information activity to Dialogporten that the notifications for correspondence {correspondenceId} have been cancelled", correspondenceId);
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
            var notificationEntities = correspondence?.Notifications ?? [];
            foreach (var notification in notificationEntities)
            {
                if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent
                backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(notification.CorrespondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCancelled, operationTimestamp));
            }
        }
    }
}
