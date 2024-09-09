namespace Altinn.Correspondence.API.Configuration;

public static class AuthorizationConstants
{
    public const string Sender = "Sender";
    public const string Recipient = "Recipient";
    public const string SenderOrRecipient = "SenderOrRecipient";

    public const string SenderScope = "altinn:correspondence.write";
    public const string RecipientScope = "altinn:correspondence.read";
}