using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Notifications;

public class EmailTemplate
{
    public string FromAddress { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public EmailContentType ContentType { get; set; } = EmailContentType.Plain;
}