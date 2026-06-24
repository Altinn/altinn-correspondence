using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.MigrateNotificationEventsBatch;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingController.Maintenance;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class CleanupMissingSyncedNotificationEventsTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _maintenanceClient;
    private readonly JsonSerializerOptions _responseSerializerOptions;
    private const string MaintenanceControllerBaseUrl = "correspondence/api/v1/maintenance";

    public CleanupMissingSyncedNotificationEventsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _maintenanceClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.MaintenanceScope));

        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_WithDefaultParameters_ReturnsOk()
    {
        // Arrange
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events";

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        Assert.True(result.TryGetProperty("message", out var message));
        Assert.Equal("Notification events cleanup started", message.GetString());
        Assert.True(result.TryGetProperty("batchCount", out var batchCount));
        Assert.Equal(100, batchCount.GetInt32());
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_WithCustomBatchCount_UsesProvidedValue()
    {
        // Arrange
        var customBatchCount = 50;
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount={customBatchCount}";

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        Assert.True(result.TryGetProperty("batchCount", out var batchCount));
        Assert.Equal(customBatchCount, batchCount.GetInt32());
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_BatchSize50_2NotificationEvents_ProcessedSuccessfully()
    {
        // Arrange
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount=50";

        // Create test correspondences with notifications that need cleanup
        var correspondence1 = await CreateCorrespondenceWithSyncedNotifications(2, new DateTime(2024, 1, 5));
        var correspondence2 = await CreateCorrespondenceWithSyncedNotifications(3, new DateTime(2024, 1, 10));

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        // Verify all expected properties are present
        Assert.True(result.TryGetProperty("message", out var message));
        Assert.Equal("Notification events cleanup started", message.GetString());

        Assert.True(result.TryGetProperty("batchCount", out var batchCount));
        Assert.Equal(50, batchCount.GetInt32());

        Assert.True(result.TryGetProperty("startingFrom", out _));
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_VerifiesHangfireJobsEnqueued()
    {
        // Arrange - Create test correspondences with notifications first
        var correspondence1 = await CreateCorrespondenceWithSyncedNotifications(2, new DateTime(2024, 1, 5));

        // Mark the notifications as synced (simulate sync job having run)
        using (var updateScope = _factory.Services.CreateScope())
        {
            var dbContext = updateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationsToSync = await dbContext.CorrespondenceNotifications
                .Where(n => n.CorrespondenceId == correspondence1)
                .ToListAsync();

            foreach (var notification in notificationsToSync)
            {
                notification.SyncedFromAltinn2 = DateTimeOffset.UtcNow.AddDays(-1);
            }

            await dbContext.SaveChangesAsync();
        }

        // Create a mock IBackgroundJobClient to capture enqueued jobs
        var mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var enqueuedJobs = new List<(Type serviceType, string methodName)>();

        // Capture all Create calls (Enqueue extension method calls Create internally)
        mockBackgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns<Job, IState>((job, state) =>
            {
                enqueuedJobs.Add((job.Type, job.Method.Name));
                return Guid.NewGuid().ToString();
            });

        // Get the handler dependencies from the factory
        // Keep scope alive until after handler completes to prevent DbContext disposal
        using var scope = _factory.Services.CreateScope();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceNotificationRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MigrateNotificationEventsBatchHandler>>();

        // Create handler with mocked IBackgroundJobClient
        var handler = new MigrateNotificationEventsBatchHandler(
            notificationRepository,
            mockBackgroundJobClient.Object,
            logger);

        // Act - Call the handler directly with a batch size that will process our notifications
        await handler.Process(batchCount: 50, lastProcessedTimestamp: DateTimeOffset.MaxValue, lastProcessedId: null);

        // Assert - Verify that jobs were enqueued (scope still alive here)
        Assert.NotEmpty(enqueuedJobs);

        // Should have enqueued AddNotificationActivity jobs for notifications
        // (may include notifications from other tests in shared database, so we check for "at least")
        var addNotificationActivityJobs = enqueuedJobs
            .Where(j => j.serviceType == typeof(IDialogportenService) && j.methodName == nameof(IDialogportenService.AddNotificationActivity))
            .ToList();

        Assert.True(addNotificationActivityJobs.Count >= 2, 
            $"Expected at least 2 AddNotificationActivity jobs, but got {addNotificationActivityJobs.Count}");

        // Should also have enqueued the next batch processing job
        var nextBatchJobs = enqueuedJobs
            .Where(j => j.serviceType == typeof(MigrateNotificationEventsBatchHandler) && j.methodName == nameof(MigrateNotificationEventsBatchHandler.Process))
            .ToList();

        Assert.Single(nextBatchJobs);
    }

    private async Task<Guid> CreateCorrespondenceWithSyncedNotifications(int notificationCount, DateTime notificationSentDate)
    {
        // Create a migrated correspondence with Altinn2 notification history
        var migrateCorrespondence = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        // Add notification history with Altinn2 data
        var notifications = new List<API.Models.MigrateCorrespondenceNotificationExt>();
        for (int i = 0; i < notificationCount; i++)
        {
            notifications.Add(new API.Models.MigrateCorrespondenceNotificationExt
            {
                Altinn2NotificationId = 1000 + i,
                NotificationAddress = $"test{i}@example.com",
                NotificationChannel = API.Models.Enums.NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(notificationSentDate.AddHours(i)),
                IsReminder = false
            });
        }
        migrateCorrespondence.NotificationHistory = notifications;

        // Use migration endpoint to create correspondence with Altinn2 data
        var migrationClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.MigrateScope));
        var migrateResponse = await migrationClient.PostAsJsonAsync(
            "correspondence/api/v1/migration/correspondence",
            migrateCorrespondence,
            _responseSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, migrateResponse.StatusCode);

        var responseContent = await migrateResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<API.Models.CorrespondenceMigrationStatusExt>(responseContent, _responseSerializerOptions);

        return result!.CorrespondenceId;
    }
}
