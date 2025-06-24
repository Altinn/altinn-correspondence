namespace Altinn.Correspondence.Core.Models.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Custom JSON converter for NotificationStatusV2 that defaults to Unknown when parsing fails
/// </summary>
public class NotificationStatusV2Converter : JsonConverter<NotificationStatusV2>
{
    private static readonly ILogger _logger = NullLogger.Instance;

    public override NotificationStatusV2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                _logger.LogWarning("Unexpected token type {TokenType} for NotificationStatusV2, expected String", reader.TokenType);
                return NotificationStatusV2.Unknown;
            }

            string enumValue = reader.GetString();
            if (string.IsNullOrEmpty(enumValue))
            {
                _logger.LogWarning("Empty or null value received for NotificationStatusV2");
                return NotificationStatusV2.Unknown;
            }

            if (Enum.TryParse<NotificationStatusV2>(enumValue, true, out NotificationStatusV2 result))
            {
                return result;
            }

            _logger.LogWarning("Failed to parse NotificationStatusV2: {Value}", enumValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while parsing NotificationStatusV2");
        }
        
        return NotificationStatusV2.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, NotificationStatusV2 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Statuses for notifications that is defined in the Altinn Notification Service
/// More information: https://github.com/Altinn/altinn-notifications/issues/747#issuecomment-2802132260
/// </summary>
[JsonConverter(typeof(NotificationStatusV2Converter))]
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
    Email_Failed_TTL = 13,

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
    SMS_Failed_RecipientReserved = 112,
    SMS_Failed_RecipientNotIdentified = 113,
    SMS_Failed_TTL = 114,
    Unknown = 999
}