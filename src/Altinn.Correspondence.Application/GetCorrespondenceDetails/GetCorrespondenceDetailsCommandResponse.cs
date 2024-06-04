using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetailsCommand;

public class GetCorrespondenceDetailsCommandResponse
{
    public required Guid CorrespondenceId { get; set; }

    public CorrespondenceStatus Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTimeOffset StatusChanged { get; set; }

    public string? Name { get; set; } = string.Empty;

    public string SendersReference { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public List<CorrespondenceReplyOptionEntity> ReplyOptions { get; set; } = new List<CorrespondenceReplyOptionEntity>();

    public List<CorrespondenceNotificationEntity> Notifications { get; set; } = new List<CorrespondenceNotificationEntity>();

    public List<CorrespondenceStatusEntity> StatusHistory { get; set; } = new List<CorrespondenceStatusEntity>();

    public Guid ResourceId { get; set; }

    public DateTimeOffset VisibleFrom { get; set; }

    public bool IsReservable { get; set; }


}

