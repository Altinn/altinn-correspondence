using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class LegacyGetCorrespondenceOverviewResponse
{
    public bool AllowDelete { get; set; }
    public bool AuthorizedForWrite { get; set; }
    public bool AuthorizedForSign { get; set; }
    public DateTimeOffset? Archived { get; set; }
    public DateTimeOffset? Confirmed { get; set; }
    public int MinimumAuthenticationLevel { get; set; }
    public int InstanceOwnerPartyId { get; set; }
    public required Guid CorrespondenceId { get; set; }

    public required CorrespondenceStatus Status { get; set; }

    public required string StatusText { get; set; }

    public required DateTimeOffset StatusChanged { get; set; }

    public string SendersReference { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string MessageSender { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public required string Language { get; set; }

    public required string MessageTitle { get; set; }

    public required string MessageSummary { get; set; }

    public required string MessageBody { get; set; }

    public required List<CorrespondenceAttachmentEntity> Attachments { get; set; }

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