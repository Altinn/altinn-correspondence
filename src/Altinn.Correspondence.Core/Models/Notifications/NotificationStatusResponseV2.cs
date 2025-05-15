namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationStatusResponseV2
{
    public Guid ShipmentId { get; set; }
    public string SendersReference { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset LastUpdate { get; set; }
    public List<RecipientStatus> Recipients { get; set; } = [];
}

public class RecipientStatus
{
    public string Type { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset LastUpdate { get; set; }
} 