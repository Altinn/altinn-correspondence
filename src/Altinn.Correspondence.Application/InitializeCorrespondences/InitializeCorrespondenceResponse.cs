using Altinn.Correspondence.Core.Models.Enums;

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
    public bool IsReminder { get; set; }
    public NotificationStatus Status { get; set; }
}
public enum NotificationStatus 
{
    Success,
    MissingContact,
    Failure,
}