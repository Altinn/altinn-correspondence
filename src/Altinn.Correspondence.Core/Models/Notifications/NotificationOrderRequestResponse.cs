namespace Altinn.Correspondence.Core.Models.Notifications;


public class NotificationOrderRequestResponse
{
    public Guid? OrderId { get; set; }

    public RecipientLookupResult? RecipientLookup { get; set; }
}