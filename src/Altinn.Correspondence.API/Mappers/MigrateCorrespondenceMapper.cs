using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class MigrateCorrespondenceMapper
{
    internal static async Task<MigrateCorrespondenceRequest> MapToRequestAsync(MigrateCorrespondenceExt migrateCorrespondenceExt, ServiceOwnerHelper serviceOwnerHelper, CancellationToken cancellationToken)
    {
        var publishedTime = migrateCorrespondenceExt.EventHistory.FirstOrDefault(e => e.Status == MigrateCorrespondenceStatusExt.Published)?.StatusChanged;

        var correspondence = new CorrespondenceEntity
        {
            Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId,
            Statuses = MapMigrateCorrespondenceStatusesExtToInternal(migrateCorrespondenceExt.EventHistory),
            Notifications = MapNotificationsToInternal(migrateCorrespondenceExt.NotificationHistory),
            ForwardingEvents = MapForwardingEventsToInternal(migrateCorrespondenceExt.ForwardingHistory),
            SendersReference = migrateCorrespondenceExt.CorrespondenceData.Correspondence.SendersReference,
            Recipient = migrateCorrespondenceExt.CorrespondenceData.Recipients.First().ToLowerInvariant(),
            RecipientType = CorrespondenceEntity.ComputeRecipientType(migrateCorrespondenceExt.CorrespondenceData.Recipients.First()),
            ResourceId = migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId,
            Sender = migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender,
            ServiceOwnerId = await serviceOwnerHelper.GetSafeServiceOwnerIdAsync(migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender, cancellationToken),
            MessageSender = migrateCorrespondenceExt.CorrespondenceData.Correspondence.MessageSender,
            RequestedPublishTime = (DateTimeOffset)migrateCorrespondenceExt.CorrespondenceData.Correspondence.RequestedPublishTime,
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
            Published = publishedTime
        };

        var deleteEvents = MapCorrespondenceStatusEventsToDeleteEvents(migrateCorrespondenceExt.EventHistory);

        return new MigrateCorrespondenceRequest()
        {
            CorrespondenceEntity = correspondence,
            Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId,
            ExistingAttachments = migrateCorrespondenceExt.CorrespondenceData.ExistingAttachments ?? new List<Guid>(),
            MakeAvailable = migrateCorrespondenceExt.MakeAvailable,
            DeleteEventEntities = deleteEvents
        };
    }

    private static List<CorrespondenceDeleteEventEntity> MapCorrespondenceStatusEventsToDeleteEvents(List<MigrateCorrespondenceStatusEventExt> eventHistory)
    {
        // Deduplicate delete events based on Status, StatusChanged (truncated to second), and EventUserPartyUuid to handle duplicate source data
        // Truncation to second is necessary because Altinn 2 uses multiple data sources with microsecond differences
        return eventHistory
            .Where(e => e.Status is MigrateCorrespondenceStatusExt.SoftDeletedByRecipient 
            or MigrateCorrespondenceStatusExt.RestoredByRecipient 
            or MigrateCorrespondenceStatusExt.PurgedByRecipient // Should never occur as we do not migrate hard deletes
            or MigrateCorrespondenceStatusExt.PurgedByAltinn)  // Should never occur as we do not migrate hard deletes
            .GroupBy(e => new { e.Status, StatusChanged = e.StatusChanged.TruncateToSecondUtc(), e.EventUserPartyUuid })
            .Select(g => g.First())
            .Select(MapCorrespondenceStatusEventToDeleteEvent)
            .ToList();
    }

    private static List<CorrespondenceStatusEntity> MapMigrateCorrespondenceStatusesExtToInternal(List<MigrateCorrespondenceStatusEventExt> eventHistory)
    {
        // Filter out delete events as these are represented as deletion events in the internal model, and should not be added as status events
        // Also deduplicate status events based on Status, StatusChanged (truncated to second), and EventUserPartyUuid to handle duplicate source data
        // Truncation to second is necessary because Altinn 2 uses multiple data sources with microsecond differences
        return eventHistory
            .Where(e => e.Status is not (MigrateCorrespondenceStatusExt.SoftDeletedByRecipient 
            or MigrateCorrespondenceStatusExt.RestoredByRecipient 
            or MigrateCorrespondenceStatusExt.PurgedByRecipient 
            or MigrateCorrespondenceStatusExt.PurgedByAltinn))
            .GroupBy(e => new { e.Status, StatusChanged = e.StatusChanged.TruncateToSecondUtc(), e.EventUserPartyUuid })
            .Select(g => g.First())
            .Select(MapCorrespondenceStatusEventToInternal)
            .ToList();
    }

    private static List<CorrespondenceNotificationEntity> MapNotificationsToInternal(List<MigrateCorrespondenceNotificationExt> notificationHistory)
    {
        // Deduplicate notifications based on NotificationSent (truncated to second), NotificationChannel, Altinn2NotificationId, NotificationAddress, and IsReminder
        // Truncation to second is necessary because Altinn 2 uses multiple data sources with microsecond differences
        return notificationHistory
            .GroupBy(n => new { NotificationSent = n.NotificationSent.TruncateToSecondUtc(), n.NotificationChannel, n.Altinn2NotificationId, n.NotificationAddress, n.IsReminder })
            .Select(g => g.First())
            .Select(MapNotificationToInternal)
            .ToList();
    }

    private static List<CorrespondenceForwardingEventEntity> MapForwardingEventsToInternal(List<MigrateCorrespondenceForwardingEventExt> forwardingHistory)
    {
        // Deduplicate forwarding events based on ForwardedOnDate (truncated to second), ForwardedByPartyUuid, ForwardedToUserId, and ForwardedToUserUuid
        // Truncation to second is necessary because Altinn 2 uses multiple data sources with microsecond differences
        return forwardingHistory
            .GroupBy(f => new { ForwardedOnDate = f.ForwardedOnDate.TruncateToSecondUtc(), f.ForwardedByPartyUuid, f.ForwardedToUserId, f.ForwardedToUserUuid })
            .Select(g => g.First())
            .Select(MapForwardingEventToInternal)
            .ToList();
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
            CorrespondenceIds = maExt.CorrespondenceIds,
            AsyncProcessing = maExt.AsyncProcessing,
            BatchSize = maExt.BatchSize,
            CreatedFrom = maExt.CreatedFrom,
            CreatedTo = maExt.CreatedTo
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
        SyncCorrespondenceStatusEventRequest requestInt = new SyncCorrespondenceStatusEventRequest
        {
            CorrespondenceId = requestExt.CorrespondenceId,
        };

        if (requestExt.SyncedEvents == null || requestExt.SyncedEvents.Count == 0)
        {
            return requestInt;
        }

        // Deduplicate events before mapping - Altinn 2 uses multiple data sources with microsecond differences
        var deduplicatedEvents = requestExt.SyncedEvents
            .GroupBy(e => new { e.Status, StatusChanged = e.StatusChanged.TruncateToSecondUtc(), e.EventUserPartyUuid })
            .Select(g => g.First())
            .ToList();

        foreach (var syncedEvent in deduplicatedEvents)
        {
            if(syncedEvent.Status == MigrateCorrespondenceStatusExt.SoftDeletedByRecipient 
                || syncedEvent.Status == MigrateCorrespondenceStatusExt.RestoredByRecipient
                || syncedEvent.Status == MigrateCorrespondenceStatusExt.PurgedByRecipient
                || syncedEvent.Status == MigrateCorrespondenceStatusExt.PurgedByAltinn)
            {
                if(requestInt.SyncedDeleteEvents == null)
                {
                    requestInt.SyncedDeleteEvents = new List<CorrespondenceDeleteEventEntity>();
                }
                requestInt.SyncedDeleteEvents.Add(MapCorrespondenceStatusEventToDeleteEvent(syncedEvent));
            }
            else
            {
                if (requestInt.SyncedEvents == null)
                {
                    requestInt.SyncedEvents = new List<CorrespondenceStatusEntity>();
                }
                requestInt.SyncedEvents.Add(MapCorrespondenceStatusEventToInternal(syncedEvent));
            }
        }

        return requestInt;
    }

    private static CorrespondenceDeleteEventType MapMigrateCorrespondenceStatusExtToDeleteEventType(MigrateCorrespondenceStatusExt status)
    {
        switch (status)
        {
            case MigrateCorrespondenceStatusExt.SoftDeletedByRecipient:
                return CorrespondenceDeleteEventType.SoftDeletedByRecipient;
            case MigrateCorrespondenceStatusExt.RestoredByRecipient:
                return CorrespondenceDeleteEventType.RestoredByRecipient;
            case MigrateCorrespondenceStatusExt.PurgedByRecipient:
                return CorrespondenceDeleteEventType.HardDeletedByRecipient;
            case MigrateCorrespondenceStatusExt.PurgedByAltinn:
                return CorrespondenceDeleteEventType.HardDeletedByServiceOwner;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}");
        }
    }

    internal static SyncCorrespondenceForwardingEventRequest MapSyncForwardingEventToInternal(SyncCorrespondenceForwardingEventRequestExt requestExt)
    {
        // Deduplicate forwarding events before mapping - Altinn 2 uses multiple data sources with microsecond differences
        var deduplicatedEvents = requestExt.SyncedEvents
            .GroupBy(f => new { ForwardedOnDate = f.ForwardedOnDate.TruncateToSecondUtc(), f.ForwardedByPartyUuid, f.ForwardedToUserId, f.ForwardedToUserUuid })
            .Select(g => g.First());

        return new SyncCorrespondenceForwardingEventRequest()
        {
            CorrespondenceId = requestExt.CorrespondenceId,
            SyncedEvents = [.. deduplicatedEvents.Select(MapForwardingEventToInternal)]
        };
    }

    internal static SyncCorrespondenceNotificationEventRequest MapSyncCorrespondenceNotificationEventToInternal(SyncCorrespondenceNotificationEventRequestExt requestExt)
    {
        // Deduplicate notification events before mapping - Altinn 2 uses multiple data sources with microsecond differences
        var deduplicatedEvents = requestExt.SyncedEvents
            .GroupBy(n => new { NotificationSent = n.NotificationSent.TruncateToSecondUtc(), n.NotificationChannel, n.Altinn2NotificationId, n.NotificationAddress, n.IsReminder })
            .Select(g => g.First());

        return new SyncCorrespondenceNotificationEventRequest()
        {
            CorrespondenceId = requestExt.CorrespondenceId,
            SyncedEvents = [.. deduplicatedEvents.Select(MapNotificationToInternal)]
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

    private static CorrespondenceDeleteEventEntity MapCorrespondenceStatusEventToDeleteEvent(MigrateCorrespondenceStatusEventExt statusEventExt)
    {
        return new CorrespondenceDeleteEventEntity
        {
            EventType = MapMigrateCorrespondenceStatusExtToDeleteEventType(statusEventExt.Status),
            EventOccurred = statusEventExt.StatusChanged,
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
