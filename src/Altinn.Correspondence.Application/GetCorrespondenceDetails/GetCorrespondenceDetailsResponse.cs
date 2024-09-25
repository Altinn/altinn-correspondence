using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public class GetCorrespondenceDetailsResponse
{
    public required Guid CorrespondenceId { get; set; }

    public CorrespondenceStatus Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTimeOffset StatusChanged { get; set; }

    public string SendersReference { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string MessageSender { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public required CorrespondenceContentEntity Content { get; set; }

    public List<CorrespondenceReplyOptionEntity> ReplyOptions { get; set; } = new List<CorrespondenceReplyOptionEntity>();

    public List<CorrespondenceNotificationEntity> Notifications { get; set; } = new List<CorrespondenceNotificationEntity>();

    public List<CorrespondenceStatusEntity> StatusHistory { get; set; } = new List<CorrespondenceStatusEntity>();

    public List<ExternalReferenceEntity> ExternalReferences { get; set; } = new List<ExternalReferenceEntity>();

    public string ResourceId { get; set; } = string.Empty;

    public DateTimeOffset VisibleFrom { get; set; }

    public bool IsReservable { get; set; }

    public bool? MarkedUnread { get; set; }

    public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

    public DateTimeOffset DueDateTime { get; set; }

    public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
}
