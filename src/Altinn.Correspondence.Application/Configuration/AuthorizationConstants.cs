namespace Altinn.Correspondence.Application.Configuration;

public static class AuthorizationConstants
{
    public const string Sender = "Sender";
    public const string Recipient = "Recipient";
    public const string SenderOrRecipient = "SenderOrRecipient";
    public const string Migrate = "Migrate";
    public const string NotificationCheck = "NotificationCheck";
    public const string SenderScope = "altinn:correspondence.write";
    public const string RecipientScope = "altinn:correspondence.read";
    public const string MigrateScope = "altinn:correspondence.migrate";
    public const string NotificationCheckScope = "altinn:system/notifications.condition.check";
}