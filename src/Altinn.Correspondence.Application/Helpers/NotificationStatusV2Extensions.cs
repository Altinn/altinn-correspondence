using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.Helpers;

public static class NotificationStatusV2Extensions
{
    public static bool IsFailed(this NotificationStatusV2 status) => status switch
    {
        >= NotificationStatusV2.Email_Failed and <= NotificationStatusV2.Email_Failed_TTL => true,
        >= NotificationStatusV2.SMS_Failed and <= NotificationStatusV2.SMS_Failed_TTL => true,
        _ => false
    };
}
