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

    /// <summary>
    /// TTL failures mean the provider's time-to-live expired without a delivery confirmation,
    /// i.e. we could not confirm delivery - as opposed to a hard failure where it could not be delivered.
    /// </summary>
    public static bool IsTtlFailure(this NotificationStatusV2 status) => status switch
    {
        NotificationStatusV2.Email_Failed_TTL => true,
        NotificationStatusV2.SMS_Failed_TTL => true,
        _ => false
    };
}
