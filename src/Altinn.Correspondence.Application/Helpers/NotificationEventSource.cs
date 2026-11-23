using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Application.Helpers;

/// <summary>
/// The resource and sender that events for a notification order should be published under.
/// Notification orders created for a confidential reminder are persisted on the correspondence that triggered the reminder,
/// but belong to the reminder resource, so events must not be published on the correspondence's resource.
/// </summary>
public record NotificationEventSource(string ResourceId, string Sender, bool IsConfidentialReminder)
{
    private static readonly string ConfidentialReminderResourceUrn = $"{UrnConstants.Resource}:{ConfidentialReminderConstants.ResourceId}";

    public static NotificationEventSource From(CorrespondenceEntity correspondence, NotificationOrderRequestV2? orderRequest)
    {
        if (orderRequest is not null && IsConfidentialReminderOrder(orderRequest))
        {
            return new NotificationEventSource(ConfidentialReminderConstants.ResourceId, ConfidentialReminderConstants.Sender, true);
        }
        return new NotificationEventSource(correspondence.ResourceId, correspondence.Sender, false);
    }

    private static bool IsConfidentialReminderOrder(NotificationOrderRequestV2 orderRequest)
    {
        var recipient = orderRequest.Recipient;
        var resourceId = recipient?.RecipientOrganization?.ResourceId
            ?? recipient?.RecipientPerson?.ResourceId
            ?? recipient?.RecipientExternalIdentity?.ResourceId;
        return resourceId == ConfidentialReminderResourceUrn;
    }
}
