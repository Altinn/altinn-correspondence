namespace Altinn.Correspondence.Core.Notifications;

public class ComposedEmailRequest
{
    public Guid IdempotencyId { get; set; }
    public string? SendersReference { get; set; }
    public DateTimeOffset RequestedSendTime { get; set; }
    public required ComposedEmailRecipient Recipient { get; set; }
}

public class ComposedEmailRecipient
{
    public required string EmailAddress { get; set; }
    public required ComposedEmailRecipientSettings EmailSettings { get; set; }
}

public class ComposedEmailRecipientSettings
{
    public string? SenderEmailAddress { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public required string ContentType { get; set; }
    public List<ComposedEmailRecipientAttachment>? Attachments { get; set; }
}

public class ComposedEmailRecipientAttachment
{
    public required string FileName { get; set; }
    public required string MimeType { get; set; }
    public required string SasUrl { get; set; }
}
