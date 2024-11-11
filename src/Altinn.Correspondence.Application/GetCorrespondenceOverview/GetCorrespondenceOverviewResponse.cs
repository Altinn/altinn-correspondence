using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewResponse
{
    public required Guid CorrespondenceId { get; set; }

    public required CorrespondenceStatus Status { get; set; }

    public required string StatusText { get; set; }

    public required DateTimeOffset StatusChanged { get; set; }

    public string? Name { get; set; } = string.Empty;

    public string SendersReference { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string MessageSender { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public required CorrespondenceContentEntity Content { get; set; }

    public List<CorrespondenceReplyOptionEntity> ReplyOptions { get; set; } = new List<CorrespondenceReplyOptionEntity>();

    public List<CorrespondenceNotificationOverview> Notifications { get; set; } = new List<CorrespondenceNotificationOverview>();

    public List<ExternalReferenceEntity> ExternalReferences { get; set; } = new List<ExternalReferenceEntity>();

    public string ResourceId { get; set; }

    public DateTimeOffset RequestedPublishTime { get; set; }

    public bool IgnoreReservation { get; set; }

    public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

    public DateTimeOffset? DueDateTime { get; set; }

    public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
    
    public DateTimeOffset? Published { get; set; }

    public bool IsConfirmationNeeded { get; set; }
}

public class CorrespondenceNotificationOverview {
    public Guid? NotificationOrderId { get; set; }
    public bool IsReminder { get; set; }
}