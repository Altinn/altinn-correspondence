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
            MessageSender = correspondenceOverview.MessageSender,
            Created = correspondenceOverview.Created,
            Recipient = correspondenceOverview.Recipient,
            Content = CorrespondenceContentMapper.MapToExternal(correspondenceOverview.Content),
            ReplyOptions = CorrespondenceReplyOptionsMapper.MapListToExternal(correspondenceOverview.ReplyOptions),
            ExternalReferences = ExternalReferenceMapper.MapListToExternal(correspondenceOverview.ExternalReferences),
            ResourceId = correspondenceOverview.ResourceId.ToString(),
            RequestedPublishTime = correspondenceOverview.RequestedPublishTime,
            MarkedUnread = correspondenceOverview.MarkedUnread,
            AllowSystemDeleteAfter = correspondenceOverview.AllowSystemDeleteAfter,
            DueDateTime = correspondenceOverview.DueDateTime,
            PropertyList = correspondenceOverview.PropertyList,
            IgnoreReservation = correspondenceOverview.IgnoreReservation,
            Published = correspondenceOverview.Published
        };
        return Correspondence;
    }
}
