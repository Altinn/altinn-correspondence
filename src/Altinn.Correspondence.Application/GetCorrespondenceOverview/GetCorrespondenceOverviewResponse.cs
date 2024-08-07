using Altinn.Correspondence.Core.Models;
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

    public List<CorrespondenceNotificationEntity> Notifications { get; set; } = new List<CorrespondenceNotificationEntity>();

    public Guid ResourceId { get; set; }

    public DateTimeOffset VisibleFrom { get; set; }

    public bool IsReservable { get; set; }

    public bool? MarkedUnread { get; set; }
}
