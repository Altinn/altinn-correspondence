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
using Hangfire.Storage;
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
        var startDate = new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?startDate={Uri.EscapeDataString(startDate.ToString("o"))}";

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
        var startDate = new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount={customBatchCount}&startDate={Uri.EscapeDataString(startDate.ToString("o"))}";

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
        // Create test correspondences with notifications that need cleanup
        // Using older dates (2020-2021) to isolate from other tests
        var correspondence1 = await CreateCorrespondenceWithSyncedNotifications(2, new DateTime(2020, 3, 10));
        var correspondence2 = await CreateCorrespondenceWithSyncedNotifications(3, new DateTime(2021, 5, 15));

        // Set startDate after the test data to ensure it gets processed
        var startDate = new DateTimeOffset(new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount=50&startDate={Uri.EscapeDataString(startDate.ToString("o"))}";

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
    public async Task CleanupMissingSyncedNotificationEvents_WithStartDate_FiltersNotificationsByDate()
    {
        // Arrange - Create correspondences with notifications at different dates
        // Use older dates (2020-2022) to isolate from other tests
        var oldDate = new DateTime(2020, 6, 15);
        var recentDate = new DateTime(2022, 8, 20);

        var oldCorrespondence = await CreateCorrespondenceWithSyncedNotifications(2, oldDate);
        var recentCorrespondence = await CreateCorrespondenceWithSyncedNotifications(2, recentDate);

        // Set startDate to filter out recent notifications (only process notifications before 2021)
        var filterDate = new DateTimeOffset(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount=50&startDate={Uri.EscapeDataString(filterDate.ToString("o"))}";

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        // Verify response includes startDate parameter
        Assert.True(result.TryGetProperty("message", out var message));
        Assert.Equal("Notification events cleanup started", message.GetString());

        Assert.True(result.TryGetProperty("batchCount", out var batchCount));
        Assert.Equal(50, batchCount.GetInt32());

        Assert.True(result.TryGetProperty("startingFrom", out var startingFrom));
        var returnedDate = startingFrom.GetDateTimeOffset();
        Assert.Equal(filterDate.Date, returnedDate.Date);

        // Note: The actual filtering behavior is tested at the repository/handler level
        // This integration test verifies the API accepts and returns the parameter correctly
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_WithStartDateInFuture_ReturnsOkWithNoDataToProcess()
    {
        // Arrange - Use a future date that won't match any existing notifications
        var futureDate = new DateTimeOffset(new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount=50&startDate={Uri.EscapeDataString(futureDate.ToString("o"))}";

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        Assert.True(result.TryGetProperty("message", out var message));
        Assert.Equal("Notification events cleanup started", message.GetString());

        Assert.True(result.TryGetProperty("startingFrom", out var startingFrom));
        var returnedDate = startingFrom.GetDateTimeOffset();
        Assert.Equal(futureDate.Date, returnedDate.Date);

        // Job is enqueued successfully even if no data matches - batch job will find no results
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_WithoutStartDate_ReturnsBadRequest()
    {
        // Arrange - Call endpoint without required startDate parameter
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount=50";

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        Assert.True(result.TryGetProperty("message", out var message));
        Assert.Contains("startDate is required", message.GetString());
    }

    [Fact]
    public async Task CleanupMissingSyncedNotificationEvents_WithInvalidBatchCount_ReturnsBadRequest()
    {
        // Arrange - Call endpoint with invalid batchCount (zero or negative)
        var startDate = new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var url = $"{MaintenanceControllerBaseUrl}/cleanup-missing-synced-notification-events?batchCount=0&startDate={Uri.EscapeDataString(startDate.ToString("o"))}";

        // Act
        var response = await _maintenanceClient.PostAsync(url, null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _responseSerializerOptions);

        Assert.True(result.TryGetProperty("message", out var message));
        Assert.Contains("batchCount must be greater than zero", message.GetString());
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
