using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;

namespace Altinn.Correspondence.Mappers;

internal static class LegacyCorrespondenceOverviewMapper
{
    internal static LegacyCorrespondenceOverviewExt MapToExternal(LegacyGetCorrespondenceOverviewResponse correspondenceOverview)
    {
        var Correspondence = new LegacyCorrespondenceOverviewExt
        {
            CorrespondenceId = correspondenceOverview.CorrespondenceId,
            Status = (CorrespondenceStatusExt)correspondenceOverview.Status,
            StatusText = correspondenceOverview.StatusText,
            StatusChanged = (DateTimeOffset)correspondenceOverview.StatusChanged,
            SendersReference = correspondenceOverview.SendersReference,
            Sender = correspondenceOverview.Sender,
            MessageSender = correspondenceOverview.MessageSender,
            Created = correspondenceOverview.Created,
            Notifications = correspondenceOverview.Notifications,
            Recipient = correspondenceOverview.Recipient,
            Content = null,
            Attachments = CorrespondenceAttachmentMapper.MapListToExternal(correspondenceOverview.Attachments),
            Language = correspondenceOverview.Language,
            MessageTitle = correspondenceOverview.MessageTitle,
            MessageSummary = correspondenceOverview.MessageSummary,
            MessageBody = correspondenceOverview.MessageBody,
            ReplyOptions = CorrespondenceReplyOptionsMapper.MapListToExternal(correspondenceOverview.ReplyOptions),
            ExternalReferences = ExternalReferenceMapper.MapListToExternal(correspondenceOverview.ExternalReferences),
            ResourceId = correspondenceOverview.ResourceId.ToString(),
            RequestedPublishTime = correspondenceOverview.RequestedPublishTime,
            MarkedUnread = correspondenceOverview.MarkedUnread,
            AllowSystemDeleteAfter = correspondenceOverview.AllowSystemDeleteAfter,
            DueDateTime = correspondenceOverview.DueDateTime,
            PropertyList = correspondenceOverview.PropertyList,
            IgnoreReservation = correspondenceOverview.IgnoreReservation,
            Published = correspondenceOverview.Published,
            IsConfirmationNeeded = correspondenceOverview.IsConfirmationNeeded,
            AllowDelete = correspondenceOverview.AllowDelete,
            Archived = correspondenceOverview.Archived,
            MinimumAuthenticationLevel = correspondenceOverview.MinimumAuthenticationLevel,
            AuthorizedForSign = correspondenceOverview.AuthorizedForSign,
        };
        return Correspondence;
    }
}
