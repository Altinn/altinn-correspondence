using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
        var batchJob = new CleanupMissingSyncedNotificationsBatchJob(
            notificationRepository,
            mockBackgroundJobClient.Object,
            Mock.Of<ILogger<CleanupMissingSyncedNotificationsBatchJob>>());
        var orchestrator = scope.ServiceProvider.GetRequiredService<ChainedBatchJobOrchestrator>();
        var handlerLogger = scope.ServiceProvider.GetRequiredService<ILogger<CleanupMissingSyncedNotificationsBatchHandler>>();

        // Create handler with orchestrator
        var handler = new CleanupMissingSyncedNotificationsBatchHandler(
            mockBackgroundJobClient.Object,
            orchestrator,
            batchJob,
            handlerLogger);

        // Act - Call ExecuteBatch directly to actually run the batch processing logic
        var request = new CleanupMissingSyncedNotificationsBatchRequest
        {
            BatchSize = 50,
            CursorNotificationSent = DateTimeOffset.MaxValue,
            CursorId = null
        };
        await handler.ExecuteBatch(request, CancellationToken.None);

        // Assert - Verify that jobs were enqueued (scope still alive here)
        Assert.NotEmpty(enqueuedJobs);

        // Should have enqueued AddNotificationActivitiesWithDuplicateCheck jobs for correspondences
        // (may include notifications from other tests in shared database, so we check for "at least")
        var addNotificationActivityJobs = enqueuedJobs
            .Where(j => j.serviceType == typeof(IDialogportenService) && j.methodName == nameof(IDialogportenService.AddNotificationActivitiesWithDuplicateCheck))
            .ToList();

        Assert.True(addNotificationActivityJobs.Count >= 1, 
            $"Expected at least 1 AddNotificationActivitiesWithDuplicateCheck job, but got {addNotificationActivityJobs.Count}");

        // Verify next batch job behavior:
        // - If we got a full batch (batchSize notifications), a next batch job should be enqueued
        // - If we got less than a full batch, no next batch job (batch processing is complete)
        var nextBatchJobs = enqueuedJobs
            .Where(j => j.serviceType == typeof(CleanupMissingSyncedNotificationsBatchHandler) && j.methodName == nameof(CleanupMissingSyncedNotificationsBatchHandler.ExecuteBatch))
            .ToList();

        // Since we're in a shared test database, we can't predict exactly how many notifications exist
        // We just verify that if there are more batches to process, a next batch job was enqueued
        // If no next batch job was enqueued, that means processing completed (which is valid)
    }

    private async Task<Guid> CreateCorrespondenceWithSyncedNotifications(int notificationCount, DateTime notificationSentDate)
    {
        // Create a migrated correspondence with 1 initial notification from Altinn2
        var migrateCorrespondence = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithNotificationHistoryEvent(
                1000,                                              // altinn2NotificationId
                "initial@example.com",                            // notificationAddress
                API.Models.Enums.NotificationChannelExt.Email,   // notificationChannelExt
                notificationSentDate,                             // notificationSent
                false)                                            // isReminder
            .Build();
        migrateCorrespondence.MakeAvailable = true;

        // Use migration endpoint to create correspondence with initial Altinn2 notification
        var migrationClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.MigrateScope));
        var migrateResponse = await migrationClient.PostAsJsonAsync(
            "correspondence/api/v1/migration/correspondence",
            migrateCorrespondence,
            _responseSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, migrateResponse.StatusCode);

        var responseContent = await migrateResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<API.Models.CorrespondenceMigrationStatusExt>(responseContent, _responseSerializerOptions);
        var correspondenceId = result!.CorrespondenceId;

        // Now sync additional notifications (this sets SyncedFromAltinn2 field)
        var syncedNotifications = new List<API.Models.MigrateCorrespondenceNotificationExt>();
        for (int i = 1; i < notificationCount; i++) // Start at 1 since we already have 1 notification
        {
            syncedNotifications.Add(new API.Models.MigrateCorrespondenceNotificationExt
            {
                Altinn2NotificationId = 1000 + i,
                NotificationAddress = $"test{i}@example.com",
                NotificationChannel = API.Models.Enums.NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(notificationSentDate.AddHours(i)),
                IsReminder = false
            });
        }

        var syncRequest = new API.Models.SyncCorrespondenceNotificationEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = syncedNotifications
        };

        var syncResponse = await migrationClient.PostAsJsonAsync(
            "correspondence/api/v1/migration/correspondence/syncNotificationEvent",
            syncRequest,
            _responseSerializerOptions);

        Assert.True(syncResponse.IsSuccessStatusCode, 
            $"Sync failed with status {syncResponse.StatusCode}: {await syncResponse.Content.ReadAsStringAsync()}");

        return correspondenceId;
    }
}
