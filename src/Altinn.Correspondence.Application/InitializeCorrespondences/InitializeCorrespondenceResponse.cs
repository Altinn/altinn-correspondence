using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesResponse
{
    public List<InializedCorrespondences> Correspondences { get; set; }
    public List<Guid> AttachmentIds { get; set; }
}

public class InializedCorrespondences
{
    public Guid CorrespondenceId { get; set; }
    public CorrespondenceStatus Status { get; set; }
    public required string Recipient { get; set; }
    public List<InitializedCorrespondencesNotifications>? Notifications { get; set; }
}
public class InitializedCorrespondencesNotifications
{
    public Guid? OrderId { get; set; }
    public bool? IsReminder { get; set; }
    public InitializedNotificationStatus Status { get; set; }
}
public enum InitializedNotificationStatus 
{
    Success,
    MissingContact,
    Failure,
}