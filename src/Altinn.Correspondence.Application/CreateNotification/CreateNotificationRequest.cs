using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Application.CreateNotification;

public class CreateNotificationRequest
{
    public required NotificationRequest NotificationRequest { get; set; }
    
    public required Guid CorrespondenceId { get; set; }

    public CorrespondenceContentEntity? CorrespondenceContent { get; set; }

    public required CorrespondenceEntity RequestCorrespondence { get; set; }
} 