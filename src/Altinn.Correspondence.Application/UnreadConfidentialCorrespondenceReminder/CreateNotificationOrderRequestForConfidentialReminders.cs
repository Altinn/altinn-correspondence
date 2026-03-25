using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Helpers.Models;

namespace Altinn.Correspondence.Application.CreateNotificationOrder;

public class CreateNotificationOrderForConfidentialReminders
{
    public required NotificationRequest NotificationRequest { get; set; }
    public required ConfidentialReminderDialogDto Reminder { get; set; }
    public required Guid CorrespondenceId { get; set; }
    public string? Language { get; set; } = null;
}
