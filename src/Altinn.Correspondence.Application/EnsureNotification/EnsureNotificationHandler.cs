using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Application.EnsureNotification;

public class EnsureNotificationHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IAltinnNotificationService altinnNotificationService) : IHandler<Guid, bool>
{
    public async Task<OneOf<bool, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            var primaryNotification = await correspondenceNotificationRepository.GetPrimaryNotification(correspondenceId, cancellationToken);
            if (primaryNotification is null)
            {
                throw new InvalidDataException("Primary notification not found");
            }
            if (primaryNotification.NotificationSent is not null)
            {
                return true;
            }
            if (primaryNotification.OrderRequest is null)
            {
                throw new ArgumentException("Order request must be set in order to retry");
            }
            var orderRequest = JsonSerializer.Deserialize<NotificationOrderRequest>(primaryNotification.OrderRequest);
            orderRequest.RequestedSendTime = DateTime.Now;
            await altinnNotificationService.CreateNotification(orderRequest, cancellationToken);
            await correspondenceNotificationRepository.WipeOrder(primaryNotification.Id, cancellationToken);
            return true;
        }
        else
        {
            throw new Exception("Correspondence is not published");
        }
    }
}
