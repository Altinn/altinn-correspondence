using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceDetailsMapper
{
    internal static CorrespondenceDetailsExt MapToExternal(GetCorrespondenceDetailsResponse correspondenceDetails)
    {
        var Correspondence = new CorrespondenceDetailsExt
        {
            CorrespondenceId = correspondenceDetails.CorrespondenceId,
            Status = (CorrespondenceStatusExt)correspondenceDetails.Status,
            StatusText = correspondenceDetails.StatusText,
            StatusChanged = (DateTimeOffset)correspondenceDetails.StatusChanged,
            SendersReference = correspondenceDetails.SendersReference,
            Sender = correspondenceDetails.Sender,
            MessageSender = correspondenceDetails.MessageSender,
            Created = correspondenceDetails.Created,
            Recipient = correspondenceDetails.Recipient,
            Content = CorrespondenceContentMapper.MapToExternal(correspondenceDetails.Content),
            ReplyOptions = correspondenceDetails.ReplyOptions != null ? CorrespondenceReplyOptionsMapper.MapListToExternal(correspondenceDetails.ReplyOptions) : new List<CorrespondenceReplyOptionExt>(),
            Notifications = correspondenceDetails.Notifications != null ? NotificationMapper.MapListToExternal(correspondenceDetails.Notifications) : new List<NotificationExt>(),
            StatusHistory = correspondenceDetails.StatusHistory != null ? CorrespondenceStatusMapper.MapListToExternal(correspondenceDetails.StatusHistory) : new List<CorrespondenceStatusEventExt>(),
            ExternalReferences = correspondenceDetails.ExternalReferences != null ? ExternalReferenceMapper.MapListToExternal(correspondenceDetails.ExternalReferences) : new List<ExternalReferenceExt>(),
            ResourceId = correspondenceDetails.ResourceId.ToString(),
            RequestedPublishTime = correspondenceDetails.RequestedPublishTime,
            IgnoreReservation = correspondenceDetails.IgnoreReservation,
            AllowSystemDeleteAfter = correspondenceDetails.AllowSystemDeleteAfter,
            DueDateTime = correspondenceDetails.DueDateTime,
            PropertyList = correspondenceDetails.PropertyList,
            Published = correspondenceDetails.Published,
            IsConfirmationNeeded = correspondenceDetails.IsConfirmationNeeded,
            IsConfidential = correspondenceDetails.IsConfidential
        };
        return Correspondence;
    }
}
