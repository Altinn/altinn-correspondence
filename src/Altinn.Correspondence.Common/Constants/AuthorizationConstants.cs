using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Altinn.Correspondence.Common.Constants;

public static class AuthorizationConstants
{
    public const string Sender = "Sender";
    public const string Recipient = "Recipient";
    public const string SenderOrRecipient = "SenderOrRecipient";
    public const string Migrate = "Migrate";
    public const string Legacy = "Legacy";
    public const string DialogportenPolicy = "DialogportenPolicy";
    public const string DialogportenScheme = "DialogportenScheme";
    public const string NotificationCheck = "NotificationCheck";
    public const string SenderScope = "altinn:correspondence.write";
    public const string RecipientScope = "altinn:correspondence.read";
    public const string MigrateScope = "altinn:correspondence.migrate";
    public const string LegacyScope = "altinn:portal/enduser";
    public const string NotificationCheckScope = "altinn:system/notifications.condition.check";
    public const string ServiceOwnerScope = "altinn:serviceowner";
    public const string MaskinportenScheme = "Maskinporten";
    public const string ArbeidsflateCors = "ArbeidsflateCors";
    public const string DownloadAttachmentPolicy = "DownloadAttachmentPolicy";
    public const string AltinnTokenOrDialogportenScheme = DialogportenScheme + "," + JwtBearerDefaults.AuthenticationScheme;
    public const string LegacyScheme = "LegacyScheme";
    public const string AllSchemes = "AllSchemes";
}