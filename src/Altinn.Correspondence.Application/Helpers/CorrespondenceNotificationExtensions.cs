using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;
using System.Text.Json;
namespace Altinn.Correspondence.Application.Helpers;
public static class CorrespondenceNotificationExtensions
{
    public static NotificationOrderRequestV2? GetOrderRequest(this CorrespondenceNotificationEntity notification)
    {
        if (notification.OrderRequest == null) return null;
        return JsonSerializer.Deserialize<NotificationOrderRequestV2>(notification.OrderRequest);
    }
}
