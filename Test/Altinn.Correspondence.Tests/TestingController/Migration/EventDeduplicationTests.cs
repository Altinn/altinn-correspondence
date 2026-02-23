using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

/// <summary>
/// Integration tests that verify the database-level deduplication mechanisms.
/// These tests exercise the unique indexes created by the AddUniqueIndexesForEventDeduplication migration.
/// </summary>
[Collection(nameof(CustomWebApplicationTestsCollection))]
public class EventDeduplicationTests : MigrationTestBase
{
    public EventDeduplicationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CorrespondenceStatus_UniqueConstraint_PreventsDuplicates()
    {
        // Arrange - Create a correspondence first
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var statusRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceStatusRepository>();

        var partyUuid = Guid.NewGuid();
        var statusChanged = new DateTime(2024, 5, 15, 14, 30, 0, DateTimeKind.Utc);
        
        var statusEvent = new CorrespondenceStatusEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            Status = CorrespondenceStatus.Read,
            StatusChanged = statusChanged,
            PartyUuid = partyUuid,
            StatusText = "Test status event"
        };

        // Act - Add the same status twice
        var firstResult = await statusRepo.AddCorrespondenceStatusForSync(statusEvent, CancellationToken.None);
        var secondResult = await statusRepo.AddCorrespondenceStatusForSync(statusEvent, CancellationToken.None);

        // Assert - First should succeed, second should return Guid.Empty
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.Equal(Guid.Empty, secondResult);
    }

    [Fact]
    public async Task CorrespondenceStatus_DifferentTimestamp_AllowsDuplicate()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var statusRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceStatusRepository>();

        var partyUuid = Guid.NewGuid();

        var statusEvent1 = new CorrespondenceStatusEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            Status = CorrespondenceStatus.Read,
            StatusChanged = new DateTime(2024, 5, 15, 14, 30, 0, DateTimeKind.Utc),
            PartyUuid = partyUuid,
            StatusText = "First read"
        };

        var statusEvent2 = new CorrespondenceStatusEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            Status = CorrespondenceStatus.Read,
            StatusChanged = new DateTime(2024, 5, 15, 14, 31, 0, DateTimeKind.Utc), // Different timestamp
            PartyUuid = partyUuid,
            StatusText = "Second read"
        };

        // Act
        var firstResult = await statusRepo.AddCorrespondenceStatusForSync(statusEvent1, CancellationToken.None);
        var secondResult = await statusRepo.AddCorrespondenceStatusForSync(statusEvent2, CancellationToken.None);

        // Assert - Both should succeed because timestamps are different
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.NotEqual(Guid.Empty, secondResult);
        Assert.NotEqual(firstResult, secondResult);
    }

    [Fact]
    public async Task CorrespondenceNotification_UniqueConstraint_PreventsDuplicates()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceNotificationRepository>();

        var notificationSent = new DateTimeOffset(new DateTime(2024, 5, 15, 10, 0, 0, DateTimeKind.Utc));

        var notification = new CorrespondenceNotificationEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            NotificationAddress = "test@example.com",
            NotificationChannel = NotificationChannel.Email,
            NotificationSent = notificationSent,
            NotificationTemplate = NotificationTemplate.CustomMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow,
            IsReminder = false
        };

        // Act - Add the same notification twice
        var firstResult = await notificationRepo.AddNotificationForSync(notification, CancellationToken.None);
        var secondResult = await notificationRepo.AddNotificationForSync(notification, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.Equal(Guid.Empty, secondResult);
    }

    [Fact]
    public async Task CorrespondenceNotification_DifferentChannel_AllowsDuplicate()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceNotificationRepository>();

        var notificationSent = new DateTimeOffset(new DateTime(2024, 5, 15, 10, 0, 0, DateTimeKind.Utc));

        var emailNotification = new CorrespondenceNotificationEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            NotificationAddress = "+4712345678",
            NotificationChannel = NotificationChannel.Email,
            NotificationSent = notificationSent,
            NotificationTemplate = NotificationTemplate.CustomMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow,
            IsReminder = false
        };

        var smsNotification = new CorrespondenceNotificationEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            NotificationAddress = "+4712345678",
            NotificationChannel = NotificationChannel.Sms, // Different channel
            NotificationSent = notificationSent,
            NotificationTemplate = NotificationTemplate.CustomMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow,
            IsReminder = false
        };

        // Act
        var firstResult = await notificationRepo.AddNotificationForSync(emailNotification, CancellationToken.None);
        var secondResult = await notificationRepo.AddNotificationForSync(smsNotification, CancellationToken.None);

        // Assert - Both should succeed because channels are different
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.NotEqual(Guid.Empty, secondResult);
        Assert.NotEqual(firstResult, secondResult);
    }

    [Fact]
    public async Task CorrespondenceForwardingEvent_UniqueConstraint_PreventsDuplicates()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var forwardingRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceForwardingEventRepository>();

        var forwardedByPartyUuid = Guid.NewGuid();
        var forwardedOnDate = new DateTimeOffset(new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc));

        var forwardingEvent = new CorrespondenceForwardingEventEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            ForwardedOnDate = forwardedOnDate,
            ForwardedByPartyUuid = forwardedByPartyUuid,
            ForwardedByUserId = 123,
            ForwardedByUserUuid = Guid.NewGuid(),
            ForwardedToEmailAddress = "forwarded@example.com"
        };

        // Act - Add the same forwarding event twice
        var firstResult = await forwardingRepo.AddForwardingEventForSync(forwardingEvent, CancellationToken.None);
        var secondResult = await forwardingRepo.AddForwardingEventForSync(forwardingEvent, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.Equal(Guid.Empty, secondResult);
    }

    [Fact]
    public async Task CorrespondenceForwardingEvent_DifferentParty_AllowsDuplicate()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var forwardingRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceForwardingEventRepository>();

        var forwardedOnDate = new DateTimeOffset(new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc));

        var forwardingEvent1 = new CorrespondenceForwardingEventEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            ForwardedOnDate = forwardedOnDate,
            ForwardedByPartyUuid = Guid.NewGuid(),
            ForwardedByUserId = 123,
            ForwardedByUserUuid = Guid.NewGuid(),
            ForwardedToEmailAddress = "user1@example.com"
        };

        var forwardingEvent2 = new CorrespondenceForwardingEventEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            ForwardedOnDate = forwardedOnDate,
            ForwardedByPartyUuid = Guid.NewGuid(), // Different party
            ForwardedByUserId = 456,
            ForwardedByUserUuid = Guid.NewGuid(),
            ForwardedToEmailAddress = "user2@example.com"
        };

        // Act
        var firstResult = await forwardingRepo.AddForwardingEventForSync(forwardingEvent1, CancellationToken.None);
        var secondResult = await forwardingRepo.AddForwardingEventForSync(forwardingEvent2, CancellationToken.None);

        // Assert - Both should succeed because parties are different
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.NotEqual(Guid.Empty, secondResult);
        Assert.NotEqual(firstResult, secondResult);
    }

    [Fact]
    public async Task CorrespondenceDeleteEvent_UniqueConstraint_PreventsDuplicates()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var deleteEventRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceDeleteEventRepository>();

        var partyUuid = Guid.NewGuid();
        var eventOccurred = new DateTimeOffset(new DateTime(2024, 5, 15, 15, 0, 0, DateTimeKind.Utc));

        var deleteEvent = new CorrespondenceDeleteEventEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
            EventOccurred = eventOccurred,
            PartyUuid = partyUuid
        };

        // Act - Add the same delete event twice
        var firstResult = await deleteEventRepo.AddDeleteEventForSync(deleteEvent, CancellationToken.None);
        var secondResult = await deleteEventRepo.AddDeleteEventForSync(deleteEvent, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.Equal(Guid.Empty, secondResult);
    }

    [Fact]
    public async Task CorrespondenceDeleteEvent_DifferentEventType_AllowsDuplicate()
    {
        // Arrange
        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .Build();

        var initializeResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result = await initializeResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var deleteEventRepo = scope.ServiceProvider.GetRequiredService<ICorrespondenceDeleteEventRepository>();

        var partyUuid = Guid.NewGuid();
        var eventOccurred = new DateTimeOffset(new DateTime(2024, 5, 15, 15, 0, 0, DateTimeKind.Utc));

        var softDeleteEvent = new CorrespondenceDeleteEventEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            EventType = CorrespondenceDeleteEventType.SoftDeletedByRecipient,
            EventOccurred = eventOccurred,
            PartyUuid = partyUuid
        };

        var restoreEvent = new CorrespondenceDeleteEventEntity
        {
            CorrespondenceId = result.CorrespondenceId,
            EventType = CorrespondenceDeleteEventType.RestoredByRecipient, // Different event type
            EventOccurred = eventOccurred,
            PartyUuid = partyUuid
        };

        // Act
        var firstResult = await deleteEventRepo.AddDeleteEventForSync(softDeleteEvent, CancellationToken.None);
        var secondResult = await deleteEventRepo.AddDeleteEventForSync(restoreEvent, CancellationToken.None);

        // Assert - Both should succeed because event types are different
        Assert.NotEqual(Guid.Empty, firstResult);
        Assert.NotEqual(Guid.Empty, secondResult);
        Assert.NotEqual(firstResult, secondResult);
    }

    [Fact]
    public async Task ReMigration_WithDuplicateEvents_AllConstraintsWork()
    {
        // Arrange - Create initial correspondence with all event types
        var partyUuid = Guid.NewGuid();
        var statusChanged = new DateTime(2024, 5, 15, 10, 0, 0, DateTimeKind.Utc);
        var notificationSent = new DateTimeOffset(new DateTime(2024, 5, 15, 11, 0, 0, DateTimeKind.Utc));
        var forwardedOnDate = new DateTimeOffset(new DateTime(2024, 5, 15, 12, 0, 0, DateTimeKind.Utc));
        var deleteOccurred = new DateTimeOffset(new DateTime(2024, 5, 15, 13, 0, 0, DateTimeKind.Utc));

        var migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, statusChanged, partyUuid)
            .WithNotificationHistoryEvent(1, "test@example.com", NotificationChannelExt.Email, notificationSent.DateTime, false)
            .WithForwardingEventHistory(new List<MigrateCorrespondenceForwardingEventExt>
            {
                new MigrateCorrespondenceForwardingEventExt
                {
                    ForwardedOnDate = forwardedOnDate,
                    ForwardedByPartyUuid = partyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = Guid.NewGuid(),
                    ForwardedToEmail = "forwarded@example.com"
                }
            })
            .WithStatusEvent(MigrateCorrespondenceStatusExt.SoftDeletedByRecipient, deleteOccurred.DateTime, partyUuid)
            .Build();

        // Initial migration
        var initializeResponse1 = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        var result1 = await initializeResponse1.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result1);

        var getDetailsResponse1 = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{result1.CorrespondenceId}/details");
        var details1 = await getDetailsResponse1.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

        // Act - Re-migrate with exact same events
        var initializeResponse2 = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        Assert.True(initializeResponse2.IsSuccessStatusCode);

        var getDetailsResponse2 = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{result1.CorrespondenceId}/details");
        var details2 = await getDetailsResponse2.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

        // Assert - All counts should remain the same (no duplicates created)
        Assert.Equal(
            details1.StatusHistory.Where(s => s.Status != CorrespondenceStatusExt.Fetched).Count(),
            details2.StatusHistory.Where(s => s.Status != CorrespondenceStatusExt.Fetched).Count());
        Assert.Equal(details1.Notifications.Count, details2.Notifications.Count);
        
        // Note: Forwarding events and delete events don't appear in CorrespondenceDetailsExt,
        // but we've verified they're caught by the constraints in the individual tests above
    }
}
