using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Altinn.Correspondence.Mappers;

internal static class MigrateCorrespondenceMapper
{
    internal static async Task<MigrateCorrespondenceRequest> MapToRequestAsync(MigrateCorrespondenceExt migrateCorrespondenceExt, ServiceOwnerHelper serviceOwnerHelper, CancellationToken cancellationToken)
    {
        var correspondence = new CorrespondenceEntity
        {
            Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId,
            Statuses = [.. migrateCorrespondenceExt.EventHistory.Select(MapCorrespondenceStatusEventToInternal)],
            Notifications = [.. migrateCorrespondenceExt.NotificationHistory.Select(MapNotificationToInternal)],
            ForwardingEvents = [.. migrateCorrespondenceExt.ForwardingHistory.Select(MapForwardingEventToInternal)],
            SendersReference = migrateCorrespondenceExt.CorrespondenceData.Correspondence.SendersReference,
            Recipient = migrateCorrespondenceExt.CorrespondenceData.Recipients.First(),
            ResourceId = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId,
            Sender = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender,
            ServiceOwnerId = await serviceOwnerHelper.GetSafeServiceOwnerIdAsync(migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender, cancellationToken),
            MessageSender = migrateCorrespondenceExt.CorrespondenceData.Correspondence.MessageSender,
            RequestedPublishTime = (DateTimeOffset)migrateCorrespondenceExt.CorrespondenceData.Correspondence.RequestedPublishTime,
            AllowSystemDeleteAfter = migrateCorrespondenceExt.CorrespondenceData.Correspondence.AllowSystemDeleteAfter,
            DueDateTime = migrateCorrespondenceExt.CorrespondenceData.Correspondence.DueDateTime,
            PropertyList = migrateCorrespondenceExt.CorrespondenceData.Correspondence.PropertyList,
            ReplyOptions = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ReplyOptions != null ? CorrespondenceReplyOptionsMapper.MapListToEntities(migrateCorrespondenceExt.CorrespondenceData.Correspondence.ReplyOptions) : new List<CorrespondenceReplyOptionEntity>(),
            IgnoreReservation = migrateCorrespondenceExt.CorrespondenceData.Correspondence.IgnoreReservation,
            ExternalReferences = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ExternalReferences != null ? ExternalReferenceMapper.MapListToEntities(migrateCorrespondenceExt.CorrespondenceData.Correspondence.ExternalReferences) : new List<ExternalReferenceEntity>(),
            Created = migrateCorrespondenceExt.Created,
            Content = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content != null ? new CorrespondenceContentEntity
            {
                Language = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.Language,
                MessageTitle = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.MessageTitle,
                MessageSummary = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.MessageSummary,
                MessageBody = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Content.MessageBody,
                Attachments = []
            } : null,
            IsConfirmationNeeded = migrateCorrespondenceExt.CorrespondenceData.Correspondence.IsConfirmationNeeded,
            IsMigrating = migrateCorrespondenceExt.IsMigrating,
            PartyId = migrateCorrespondenceExt.PartyId,
            Published = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Published
        };
        
        return new MigrateCorrespondenceRequest()
        {
            CorrespondenceEntity = correspondence,
            Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId,
            ExistingAttachments = migrateCorrespondenceExt.CorrespondenceData.ExistingAttachments ?? new List<Guid>(),
            MakeAvailable = migrateCorrespondenceExt.MakeAvailable
        };
    }

    internal static CorrespondenceMigrationStatusExt MapCorrespondenceMigrationStatusToExternal(MigrateCorrespondenceResponse migrateCorrespondenceResponse)
    {
        return new CorrespondenceMigrationStatusExt()
        {
            CorrespondenceId = migrateCorrespondenceResponse.CorrespondenceId,
            Altinn2CorrespondenceId = migrateCorrespondenceResponse.Altinn2CorrespondenceId,
            AttachmentStatuses = MapAttachmentMigrationStatusToExternal(migrateCorrespondenceResponse.AttachmentMigrationStatuses),
            DialogId = migrateCorrespondenceResponse.DialogId
        };
    }

    internal static MakeCorrespondenceAvailableRequest MapMakeAvailableToInternal(MakeCorrespondenceAvailableRequestExt maExt)
    {
        return new MakeCorrespondenceAvailableRequest()
        {
            CreateEvents = maExt.CreateEvents,
            CorrespondenceId = maExt.CorrespondenceId,
            CorrespondenceIds = maExt.CorrespondenceIds
        };
    }

    internal static MakeCorrespondenceAvailableResponseExt MapMakeAvailableResponseToExternal(MakeCorrespondenceAvailableResponse response)
    {
        return new MakeCorrespondenceAvailableResponseExt
        {
            Statuses = response.Statuses?.Select(s => new MakeCorrespondenceAvailableStatusExt(s.CorrespondenceId, s.Error, s.DialogId, s.Ok)).ToList()
        };
    }

    internal static SyncCorrespondenceStatusEventRequest MapSyncStatusEventToInternal(SyncCorrespondenceStatusEventRequestExt requestExt)
    {
        return new SyncCorrespondenceStatusEventRequest()
        {
            CorrespondenceId = requestExt.CorrespondenceId,
            SyncedEvents = [.. requestExt.SyncedEvents.Select(MapCorrespondenceStatusEventToInternal)]
        };
    }

    internal static SyncCorrespondenceForwardingEventRequest MapSyncForwardingEventToInternal(SyncCorrespondenceForwardingEventRequestExt requestExt)
    {
        return new SyncCorrespondenceForwardingEventRequest()
        {
            CorrespondenceId = requestExt.CorrespondenceId,
            SyncedEvents = [.. requestExt.SyncedEvents.Select(MapForwardingEventToInternal)]
        };
    }

    internal static SyncCorrespondenceNotificationEventRequest MapSyncCorrespondenceNotificationEventToInternal(SyncCorrespondenceNotificationEventRequestExt requestExt)
    {
        return new SyncCorrespondenceNotificationEventRequest()
        {
            CorrespondenceId = requestExt.CorrespondenceId,
            SyncedEvents = [.. requestExt.SyncedEvents.Select(MapNotificationToInternal)]
        };
    }

    private static CorrespondenceStatusEntity MapCorrespondenceStatusEventToInternal(MigrateCorrespondenceStatusEventExt statusEventExt)
    {
        return new CorrespondenceStatusEntity
        {
            Status = (CorrespondenceStatus)statusEventExt.Status,
            StatusChanged = statusEventExt.StatusChanged,
            StatusText = statusEventExt.StatusText ?? statusEventExt.Status.ToString(),
            PartyUuid = statusEventExt.EventUserPartyUuid
        };
    }

    private static CorrespondenceForwardingEventEntity MapForwardingEventToInternal(MigrateCorrespondenceForwardingEventExt forwardingEvent)
    {
        return new CorrespondenceForwardingEventEntity()
        {
            ForwardedOnDate = forwardingEvent.ForwardedOnDate,
            ForwardedByPartyUuid = forwardingEvent.ForwardedByPartyUuid,
            ForwardedByUserId = forwardingEvent.ForwardedByUserId,
            ForwardedByUserUuid = forwardingEvent.ForwardedByUserUuid,
            ForwardedToUserId = forwardingEvent.ForwardedToUserId,
            ForwardedToUserUuid = forwardingEvent.ForwardedToUserUuid,
            ForwardingText = forwardingEvent.ForwardingText,
            ForwardedToEmailAddress = forwardingEvent.ForwardedToEmail,
            MailboxSupplier = forwardingEvent.MailboxSupplier
        };
    }

    private static List<AttachmentMigrationStatusExt>? MapAttachmentMigrationStatusToExternal(List<AttachmentMigrationStatus>? attachmentMigrationStatuses)
    {
        if (attachmentMigrationStatuses == null)
            return null;

        return attachmentMigrationStatuses.Select(MapAttachmentMigrationStatusToExternal).ToList();
    }

    private static AttachmentMigrationStatusExt MapAttachmentMigrationStatusToExternal(AttachmentMigrationStatus attachmentMigrationStatus)
    {
        return new AttachmentMigrationStatusExt()
        {
            AttachmentId = attachmentMigrationStatus.AttachmentId,
            Status = (AttachmentStatusExt)attachmentMigrationStatus.AttachmentStatus
        };
    }

    private static CorrespondenceNotificationEntity MapNotificationToInternal(MigrateCorrespondenceNotificationExt notificationExt)
    {
        return new CorrespondenceNotificationEntity()
        {
            Created = notificationExt.NotificationSent,
            NotificationSent = notificationExt.NotificationSent,
            NotificationChannel = (NotificationChannel)notificationExt.NotificationChannel,
            NotificationTemplate = NotificationTemplate.Altinn2Message,
            NotificationAddress = notificationExt.NotificationAddress,
            Altinn2NotificationId = notificationExt.Altinn2NotificationId,
            IsReminder = notificationExt.IsReminder,
        };
    }
}
