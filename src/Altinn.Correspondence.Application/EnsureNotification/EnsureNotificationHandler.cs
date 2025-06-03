using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Application.EnsureNotification;

public class EnsureNotificationHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IAltinnNotificationService altinnNotificationService,
    ILogger<EnsureNotificationHandler> logger) : IHandler<Guid, bool>
{
    public async Task<OneOf<bool, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing ensure notification request for correspondence {CorrespondenceId}", correspondenceId);
        logger.LogDebug("Retrieving correspondence {CorrespondenceId} with notifications", correspondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", correspondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            logger.LogDebug("Retrieving primary notification for correspondence {CorrespondenceId}", correspondenceId);
            var primaryNotification = await correspondenceNotificationRepository.GetPrimaryNotification(correspondenceId, cancellationToken);
            if (primaryNotification is null)
            {
                logger.LogError("Primary notification not found for correspondence {CorrespondenceId}", correspondenceId);
                throw new InvalidDataException("Primary notification not found");
            }

            if (primaryNotification.NotificationSent is not null)
            {
                logger.LogInformation("Notification already sent for correspondence {CorrespondenceId}", correspondenceId);
                return true;
            }

            if (primaryNotification.OrderRequest is null)
            {
                logger.LogError("Order request is missing for correspondence {CorrespondenceId}", correspondenceId);
                throw new ArgumentException("Order request must be set in order to retry");
            }
            logger.LogDebug("Deserializing order request for correspondence {CorrespondenceId}", correspondenceId);
            var orderRequest = JsonSerializer.Deserialize<NotificationOrderRequest>(primaryNotification.OrderRequest);
            orderRequest.RequestedSendTime = DateTime.Now;
            logger.LogInformation("Creating notification for correspondence {CorrespondenceId}", correspondenceId);
            await altinnNotificationService.CreateNotification(orderRequest, cancellationToken);
            logger.LogDebug("Wiping order for notification {NotificationId}", primaryNotification.Id);
            await correspondenceNotificationRepository.WipeOrder(primaryNotification.Id, cancellationToken);
            logger.LogInformation("Successfully ensured notification for correspondence {CorrespondenceId}", correspondenceId);
            return true;
        }
        else
        {
            logger.LogWarning("Cannot ensure notification - correspondence {CorrespondenceId} is not published", correspondenceId);
            throw new Exception("Correspondence is not published");
        }
    }
}
