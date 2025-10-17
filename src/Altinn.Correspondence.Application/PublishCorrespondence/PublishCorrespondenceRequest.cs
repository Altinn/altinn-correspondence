using Altinn.Correspondence.Application.InitializeCorrespondences;

namespace Altinn.Correspondence.Application.PublishCorrespondence;

public class PublishCorrespondenceRequest
{
    public required Guid CorrespondenceId { get; set; }
    public required NotificationRequest? NotificationRequest { get; set; }
} 