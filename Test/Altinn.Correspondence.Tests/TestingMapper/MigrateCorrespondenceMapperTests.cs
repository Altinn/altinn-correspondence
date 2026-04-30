using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Mappers;
using Xunit;

namespace Altinn.Correspondence.Tests.TestingMapper;

/// <summary>
/// Tests for MigrateCorrespondenceMapper deduplication logic.
/// These tests validate that duplicate events from Altinn 2's multiple data sources are properly filtered.
/// </summary>
public class MigrateCorrespondenceMapperTests
{
    private readonly Guid _defaultCorrespondenceId = Guid.NewGuid();
    private readonly Guid _defaultUserPartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly DateTimeOffset _baseTime = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

    #region Migration Flow Tests - Status Events

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_NoDuplicates_AllEventsReturned()
    {
        // Arrange
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Confirmed, StatusChanged = _baseTime.AddMinutes(5), EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Archived, StatusChanged = _baseTime.AddMinutes(10), EventUserPartyUuid = _defaultUserPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.Status == CorrespondenceStatus.Read);
        Assert.Contains(result, e => e.Status == CorrespondenceStatus.Confirmed);
        Assert.Contains(result, e => e.Status == CorrespondenceStatus.Archived);
    }

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_ExactDuplicates_OnlyOneReturned()
    {
        // Arrange - Two events with identical timestamp
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Single(result);
        Assert.Equal(CorrespondenceStatus.Read, result[0].Status);
    }

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_DuplicatesWithinSameSecond_OnlyOneReturned()
    {
        // Arrange - Two events within same second (microsecond difference from different Altinn 2 data sources)
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime.AddMilliseconds(150), EventUserPartyUuid = _defaultUserPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Single(result);
        Assert.Equal(CorrespondenceStatus.Read, result[0].Status);
    }

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_DuplicatesOneSecondApart_BothReturned()
    {
        // Arrange - Two events one second apart should NOT be considered duplicates
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime.AddSeconds(1), EventUserPartyUuid = _defaultUserPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_SameStatusDifferentParty_BothReturned()
    {
        // Arrange - Same status and time but different party
        var otherPartyUuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = otherPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_MultipleDuplicatesOfEachStatus_OneOfEachReturned()
    {
        // Arrange - Multiple duplicates across different statuses
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime.AddMilliseconds(100), EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Confirmed, StatusChanged = _baseTime.AddMinutes(5), EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Confirmed, StatusChanged = _baseTime.AddMinutes(5).AddMilliseconds(250), EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Archived, StatusChanged = _baseTime.AddMinutes(10), EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.Archived, StatusChanged = _baseTime.AddMinutes(10).AddMilliseconds(500), EventUserPartyUuid = _defaultUserPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Single(result.Where(e => e.Status == CorrespondenceStatus.Read));
        Assert.Single(result.Where(e => e.Status == CorrespondenceStatus.Confirmed));
        Assert.Single(result.Where(e => e.Status == CorrespondenceStatus.Archived));
    }

    [Fact]
    public void MapMigrateCorrespondenceStatusEvents_DeleteEventsFiltered_NotReturned()
    {
        // Arrange - Delete events should be filtered out
        var eventHistory = new List<MigrateCorrespondenceStatusEventExt>
        {
            new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.SoftDeletedByRecipient, StatusChanged = _baseTime.AddMinutes(5), EventUserPartyUuid = _defaultUserPartyUuid },
            new() { Status = MigrateCorrespondenceStatusExt.RestoredByRecipient, StatusChanged = _baseTime.AddMinutes(10), EventUserPartyUuid = _defaultUserPartyUuid }
        };

        // Act
        var result = InvokeMapMigrateCorrespondenceStatusesExtToInternal(eventHistory);

        // Assert
        Assert.Single(result);
        Assert.Equal(CorrespondenceStatus.Read, result[0].Status);
    }

    #endregion

    #region Migration Flow Tests - Notifications

    [Fact]
    public void MapNotifications_NoDuplicates_AllReturned()
    {
        // Arrange
        var notifications = new List<MigrateCorrespondenceNotificationExt>
        {
            new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test1@example.com", IsReminder = false },
            new() { NotificationSent = _baseTime.AddMinutes(5), NotificationChannel = NotificationChannelExt.Sms, Altinn2NotificationId = 2, NotificationAddress = "+4712345678", IsReminder = false }
        };

        // Act
        var result = InvokeMapNotificationsToInternal(notifications);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MapNotifications_ExactDuplicates_OnlyOneReturned()
    {
        // Arrange
        var notifications = new List<MigrateCorrespondenceNotificationExt>
        {
            new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false },
            new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false }
        };

        // Act
        var result = InvokeMapNotificationsToInternal(notifications);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void MapNotifications_DuplicatesWithinSameSecond_OnlyOneReturned()
    {
        // Arrange
        var notifications = new List<MigrateCorrespondenceNotificationExt>
        {
            new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false },
            new() { NotificationSent = _baseTime.AddMilliseconds(500), NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false }
        };

        // Act
        var result = InvokeMapNotificationsToInternal(notifications);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void MapNotifications_SameTimeButDifferentChannel_BothReturned()
    {
        // Arrange - Same notification ID and time but different channel
        var notifications = new List<MigrateCorrespondenceNotificationExt>
        {
            new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false },
            new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Sms, Altinn2NotificationId = 1, NotificationAddress = "+4712345678", IsReminder = false }
        };

        // Act
        var result = InvokeMapNotificationsToInternal(notifications);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Migration Flow Tests - Forwarding Events

    [Fact]
    public void MapForwardingEvents_NoDuplicates_AllReturned()
    {
        // Arrange
        var forwardingEvents = new List<MigrateCorrespondenceForwardingEventExt>
        {
            new() { ForwardedOnDate = _baseTime, ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedByUserId = 1, ForwardedByUserUuid = Guid.NewGuid() },
            new() { ForwardedOnDate = _baseTime.AddMinutes(5), ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedByUserId = 2, ForwardedByUserUuid = Guid.NewGuid() }
        };

        // Act
        var result = InvokeMapForwardingEventsToInternal(forwardingEvents);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MapForwardingEvents_ExactDuplicates_OnlyOneReturned()
    {
        // Arrange
        var forwardedToUserId = 123;
        var forwardedToUserUuid = Guid.NewGuid();
        var forwardingEvents = new List<MigrateCorrespondenceForwardingEventExt>
        {
            new() { ForwardedOnDate = _baseTime, ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedToUserId = forwardedToUserId, ForwardedToUserUuid = forwardedToUserUuid },
            new() { ForwardedOnDate = _baseTime, ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedToUserId = forwardedToUserId, ForwardedToUserUuid = forwardedToUserUuid }
        };

        // Act
        var result = InvokeMapForwardingEventsToInternal(forwardingEvents);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void MapForwardingEvents_DuplicatesWithinSameSecond_OnlyOneReturned()
    {
        // Arrange
        var forwardedToUserId = 123;
        var forwardedToUserUuid = Guid.NewGuid();
        var forwardingEvents = new List<MigrateCorrespondenceForwardingEventExt>
        {
            new() { ForwardedOnDate = _baseTime, ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedToUserId = forwardedToUserId, ForwardedToUserUuid = forwardedToUserUuid },
            new() { ForwardedOnDate = _baseTime.AddMilliseconds(750), ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedToUserId = forwardedToUserId, ForwardedToUserUuid = forwardedToUserUuid }
        };

        // Act
        var result = InvokeMapForwardingEventsToInternal(forwardingEvents);

        // Assert
        Assert.Single(result);
    }

    #endregion

    #region Sync Flow Tests - Status Events

    [Fact]
    public void MapSyncStatusEvent_NoDuplicates_AllEventsReturned()
    {
        // Arrange
        var request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = _defaultCorrespondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.Confirmed, StatusChanged = _baseTime.AddMinutes(5), EventUserPartyUuid = _defaultUserPartyUuid }
            }
        };

        // Act
        var result = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);

        // Assert
        Assert.NotNull(result.SyncedEvents);
        Assert.Equal(2, result.SyncedEvents.Count);
    }

    [Fact]
    public void MapSyncStatusEvent_DuplicatesWithinSameSecond_OnlyOneReturned()
    {
        // Arrange
        var request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = _defaultCorrespondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime.AddMilliseconds(200), EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.Confirmed, StatusChanged = _baseTime.AddMinutes(5), EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.Confirmed, StatusChanged = _baseTime.AddMinutes(5).AddMilliseconds(150), EventUserPartyUuid = _defaultUserPartyUuid }
            }
        };

        // Act
        var result = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);

        // Assert
        Assert.NotNull(result.SyncedEvents);
        Assert.Equal(2, result.SyncedEvents.Count);
        Assert.Single(result.SyncedEvents.Where(e => e.Status == CorrespondenceStatus.Read));
        Assert.Single(result.SyncedEvents.Where(e => e.Status == CorrespondenceStatus.Confirmed));
    }

    [Fact]
    public void MapSyncStatusEvent_DeleteEventsDeduplicatedSeparately_Correct()
    {
        // Arrange - Delete events are mapped to SyncedDeleteEvents
        var request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = _defaultCorrespondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new() { Status = MigrateCorrespondenceStatusExt.SoftDeletedByRecipient, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.SoftDeletedByRecipient, StatusChanged = _baseTime.AddMilliseconds(300), EventUserPartyUuid = _defaultUserPartyUuid }
            }
        };

        // Act
        var result = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);

        // Assert
        Assert.NotNull(result.SyncedDeleteEvents);
        Assert.Single(result.SyncedDeleteEvents); // Duplicates should be filtered
        Assert.Null(result.SyncedEvents);
    }

    [Fact]
    public void MapSyncStatusEvent_MixedStatusAndDeleteEvents_BothDeduplicatedCorrectly()
    {
        // Arrange
        var request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = _defaultCorrespondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime, EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.Read, StatusChanged = _baseTime.AddMilliseconds(100), EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.SoftDeletedByRecipient, StatusChanged = _baseTime.AddMinutes(5), EventUserPartyUuid = _defaultUserPartyUuid },
                new() { Status = MigrateCorrespondenceStatusExt.SoftDeletedByRecipient, StatusChanged = _baseTime.AddMinutes(5).AddMilliseconds(200), EventUserPartyUuid = _defaultUserPartyUuid }
            }
        };

        // Act
        var result = MigrateCorrespondenceMapper.MapSyncStatusEventToInternal(request);

        // Assert
        Assert.NotNull(result.SyncedEvents);
        Assert.Single(result.SyncedEvents);
        Assert.NotNull(result.SyncedDeleteEvents);
        Assert.Single(result.SyncedDeleteEvents);
    }

    #endregion

    #region Sync Flow Tests - Forwarding Events

    [Fact]
    public void MapSyncForwardingEvent_DuplicatesWithinSameSecond_OnlyOneReturned()
    {
        // Arrange
        var forwardedToUserId = 123;
        var forwardedToUserUuid = Guid.NewGuid();
        var request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = _defaultCorrespondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>
            {
                new() { ForwardedOnDate = _baseTime, ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedToUserId = forwardedToUserId, ForwardedToUserUuid = forwardedToUserUuid },
                new() { ForwardedOnDate = _baseTime.AddMilliseconds(400), ForwardedByPartyUuid = _defaultUserPartyUuid, ForwardedToUserId = forwardedToUserId, ForwardedToUserUuid = forwardedToUserUuid }
            }
        };

        // Act
        var result = MigrateCorrespondenceMapper.MapSyncForwardingEventToInternal(request);

        // Assert
        Assert.Single(result.SyncedEvents);
    }

    #endregion

    #region Sync Flow Tests - Notifications

    [Fact]
    public void MapSyncNotificationEvent_DuplicatesWithinSameSecond_OnlyOneReturned()
    {
        // Arrange
        var request = new SyncCorrespondenceNotificationEventRequestExt
        {
            CorrespondenceId = _defaultCorrespondenceId,
            SyncedEvents = new List<MigrateCorrespondenceNotificationExt>
            {
                new() { NotificationSent = _baseTime, NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false },
                new() { NotificationSent = _baseTime.AddMilliseconds(600), NotificationChannel = NotificationChannelExt.Email, Altinn2NotificationId = 1, NotificationAddress = "test@example.com", IsReminder = false }
            }
        };

        // Act
        var result = MigrateCorrespondenceMapper.MapSyncCorrespondenceNotificationEventToInternal(request);

        // Assert
        Assert.Single(result.SyncedEvents);
    }

    #endregion

    #region Helper Methods

    private List<Core.Models.Entities.CorrespondenceStatusEntity> InvokeMapMigrateCorrespondenceStatusesExtToInternal(List<MigrateCorrespondenceStatusEventExt> eventHistory)
    {
        // Use reflection to call the private static method
        var method = typeof(MigrateCorrespondenceMapper).GetMethod(
            "MapMigrateCorrespondenceStatusesExtToInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { eventHistory });
        return (List<Core.Models.Entities.CorrespondenceStatusEntity>)result!;
    }

    private List<Core.Models.Entities.CorrespondenceNotificationEntity> InvokeMapNotificationsToInternal(List<MigrateCorrespondenceNotificationExt> notifications)
    {
        // Use reflection to call the private static method
        var method = typeof(MigrateCorrespondenceMapper).GetMethod(
            "MapNotificationsToInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { notifications });
        return (List<Core.Models.Entities.CorrespondenceNotificationEntity>)result!;
    }

    private List<Core.Models.Entities.CorrespondenceForwardingEventEntity> InvokeMapForwardingEventsToInternal(List<MigrateCorrespondenceForwardingEventExt> forwardingEvents)
    {
        // Use reflection to call the private static method
        var method = typeof(MigrateCorrespondenceMapper).GetMethod(
            "MapForwardingEventsToInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { forwardingEvents });
        return (List<Core.Models.Entities.CorrespondenceForwardingEventEntity>)result!;
    }

    #endregion
}
