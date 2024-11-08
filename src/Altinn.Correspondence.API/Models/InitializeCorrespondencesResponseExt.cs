using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models;

public class InitializeCorrespondencesResponseExt
{
    public List<InitializedCorrespondencesExt> Correspondences { get; set; }
    public List<Guid> AttachmentIds { get; set; }
}


public class InitializedCorrespondencesExt
{
    public Guid CorrespondenceId { get; set; }
    public CorrespondenceStatusExt Status { get; set; }
    public required string Recipient { get; set; }
    public List<InitializedCorrespondencesNotificationsExt>? Notifications { get; set; }
}
public class InitializedCorrespondencesNotificationsExt
{
    public Guid? OrderId { get; set; }
    public bool? IsReminder { get; set; }
    public InitializedNotificationStatusExt Status { get; set; }
}
public enum InitializedNotificationStatusExt
{
    Success,
    MissingContact,
    Failure,
}