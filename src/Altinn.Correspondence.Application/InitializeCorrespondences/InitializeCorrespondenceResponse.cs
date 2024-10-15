using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesResponse
{
    public List<CorrespondenceDetails> Correspondences { get; set; }
    public List<Guid> AttachmentIds { get; set; }
}

public class CorrespondenceDetails
{
    public Guid CorrespondenceId { get; set; }
    public CorrespondenceStatus Status { get; set; }
    public required string Recipient { get; set; }
    public List<NotificationDetails>? Notifications { get; set; }
}
public class NotificationDetails
{
    public Guid? OrderId { get; set; }
    public string Id { get; set; } = string.Empty;
    public bool IsReminder { get; set; }
    public StatusExt Status { get; set; }
}
public enum NotificationStatus 
{
    Success,
    MissingContact,
    Failure,
}