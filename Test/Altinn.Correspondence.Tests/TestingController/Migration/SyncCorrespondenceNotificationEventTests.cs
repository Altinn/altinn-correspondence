using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class SyncCorrespondenceNotificationEventTests : MigrationTestBase
{
    internal const string syncCorresponenceNotificationEventUrl = $"{migrateCorrespondenceControllerBaseUrl}/correspondence/syncNotificationEvent";

    public SyncCorrespondenceNotificationEventTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SyncNotificationEvent_NewReminderNotificaiton_NewSaved()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithCreatedAt(new DateTime(2024, 1, 1, 03, 09, 21))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7, 12, 0, 0, 0), false)
            .WithRecipient("urn:altinn:person:identifier-no:29909898925")
            .WithResourceId("skd-migratedcorrespondence-5229-1")
            .Build();
        migrateCorrespondenceExt.MakeAvailable = true;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceNotificationEventRequestExt request = new SyncCorrespondenceNotificationEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceNotificationExt>
            { 
                new MigrateCorrespondenceNotificationExt
                {
                    NotificationAddress = "90112233",
                    NotificationChannel = NotificationChannelExt.Sms,
                    NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    Altinn2NotificationId = 2,
                    IsReminder = true
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceNotificationEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);      
        
        Assert.Equal(2, getCorrespondenceDetails.Notifications.Count);
        Assert.Contains(getCorrespondenceDetails.Notifications, n => n.IsReminder);
    }

    [Fact]
    public async Task SyncNotificationEvent_ExistingNotification_NewNotSaved()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithCreatedAt(new DateTime(2024, 1, 1, 03, 09, 21))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7), false)
            .WithRecipient("urn:altinn:person:identifier-no:29909898925")
            .WithResourceId("skd-migratedcorrespondence-5229-1")
            .Build();
        migrateCorrespondenceExt.MakeAvailable = true;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceNotificationEventRequestExt request = new SyncCorrespondenceNotificationEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceNotificationExt>
            {
                new MigrateCorrespondenceNotificationExt
                {
                    NotificationAddress = "testemail@altinn.no",
                    NotificationChannel = NotificationChannelExt.Email,
                    NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 7)),
                    Altinn2NotificationId = 1,
                    IsReminder = false
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceNotificationEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);
        Assert.Equal(1, getCorrespondenceDetails.Notifications.Count);        
    }

    [Fact]
    public async Task SyncNotificationEvent_ExistingNotification_NewWithSlightoffsetNotSaved()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithCreatedAt(new DateTime(2024, 1, 1, 03, 09, 21))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7, 12, 0, 0, 0), false)
            .WithRecipient("urn:altinn:person:identifier-no:29909898925")
            .WithResourceId("skd-migratedcorrespondence-5229-1")
            .Build();
        migrateCorrespondenceExt.MakeAvailable = true;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceNotificationEventRequestExt request = new SyncCorrespondenceNotificationEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceNotificationExt>
            {
                new MigrateCorrespondenceNotificationExt
                {
                    NotificationAddress = "testemail@altinn.no",
                    NotificationChannel = NotificationChannelExt.Email,
                    NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 7, 12, 0, 0, 150)),
                    Altinn2NotificationId = 1,
                    IsReminder = false
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceNotificationEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);
        Assert.Equal(1, getCorrespondenceDetails.Notifications.Count);
    }


    [Fact]
    public async Task SyncNotificationEvent_NoNotificationsSpecified_HttpBadRequest()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
             .CreateMigrateCorrespondence()
             .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
             .WithCreatedAt(new DateTime(2024, 1, 1, 03, 09, 21))
             .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7, 12, 0, 0, 0), false)
             .WithRecipient("urn:altinn:person:identifier-no:29909898925")
             .WithResourceId("skd-migratedcorrespondence-5229-1")
             .Build();
        migrateCorrespondenceExt.MakeAvailable = true;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceNotificationEventRequestExt request = new SyncCorrespondenceNotificationEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceNotificationExt>()
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceNotificationEventUrl, request);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> MigrateCorrespondence(MigrateCorrespondenceExt migrateCorrespondenceExt)
    {
        var migrateResponse = await _migrationClient.PostAsJsonAsync(migrateCorrespondenceUrl, migrateCorrespondenceExt);
        Assert.True(migrateResponse.IsSuccessStatusCode);
        var resultObj = await migrateResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>();        
        return resultObj.CorrespondenceId;
    }

    private async Task<CorrespondenceDetailsExt> GetCorrespondenceDetailsAsync(Guid correspondenceId)
    {
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode);
        return await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
    }
}
