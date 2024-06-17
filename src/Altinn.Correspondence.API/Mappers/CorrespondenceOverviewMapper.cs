using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceOverviewMapper
{
    internal static CorrespondenceOverviewExt MapToExternal(GetCorrespondenceOverviewResponse correspondenceOverview)
    {
        var Correspondence = new CorrespondenceOverviewExt
        {
            CorrespondenceId = correspondenceOverview.CorrespondenceId,
            Status = (CorrespondenceStatusExt)correspondenceOverview.Status,
            StatusText = correspondenceOverview.StatusText,
            StatusChanged = (DateTimeOffset)correspondenceOverview.StatusChanged,
            SendersReference = correspondenceOverview.SendersReference,
            Sender = correspondenceOverview.Sender,
            Created = correspondenceOverview.Created,
            Recipient = correspondenceOverview.Recipient,
            ReplyOptions = CorrespondenceReplyOptionsMapper.MapListToExternal(correspondenceOverview.ReplyOptions),
            Notifications = InitializeCorrespondenceNotificationMapper.MapListToExternal(correspondenceOverview.Notifications),
            ResourceId = correspondenceOverview.ResourceId.ToString(),
            VisibleFrom = correspondenceOverview.VisibleFrom
        };
        return Correspondence;
    }
}
