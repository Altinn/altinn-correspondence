namespace Altinn.Correspondence.Core.Models.Enums;

/// <summary>
/// Statuses for notifications that is defined in the Altinn Notification Service
/// More information: https://github.com/Altinn/altinn-notifications/issues/747#issuecomment-2802132260
/// </summary>
public enum NotificationStatusV2
{
    Email_New = 0,
    Email_Sending = 1,
    Email_Succeeded = 2,
    Email_Delivered = 3,
    Email_Failed = 4,
    Email_Failed_Bounced = 5,
    Email_Failed_TransientError = 6,
    Email_Failed_Quarantined = 7,
    Email_Failed_RecipientReserved = 8,
    Email_Failed_FilteredSpam = 9,
    Email_Failed_InvalidEmailFormat = 10,
    Email_Failed_SuppressedRecipient = 11,
    Email_Failed_RecipientNotIdentified = 12,

    SMS_New = 100,
    SMS_Sending = 101,
    SMS_Accepted = 102,
    SMS_Delivered = 103,
    SMS_Failed = 104,
    SMS_Failed_Deleted = 105,
    SMS_Failed_Expired = 106,
    SMS_Failed_Rejected = 107,
    SMS_Failed_Undelivered = 108,
    SMS_Failed_InvalidReceiver = 109,
    SMS_Failed_BarredReceiver = 110,
    SMS_Failed_InvalidRecipient = 111,
    SMS_Failed_Recipientreserved = 112,
    SMS_Failed_RecipientReserved = 113,
    SMS_Failed_RecipientNotIdentified = 114
}