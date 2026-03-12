using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesResponse
{
    public List<InitializedCorrespondences> Correspondences { get; set; }
    public List<Guid> AttachmentIds { get; set; }
}

public class InitializedCorrespondences
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
    /// <summary>
    /// The recipient lookup was successful for at least one recipient
    /// </summary>
    Success,
    /// <summary>
    /// The recipient lookup failed for all recipients
    /// </summary>
    MissingContact,
    /// <summary>
    /// The notification order failed to be created due to an error
    /// </summary>
    Failure,
}