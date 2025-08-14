using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OneOf.Types;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Web;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class SyncCorrespondenceNotificationEventTests : MigrationTestBase
{
    internal const string syncCorresponenceNotificationEventUrl = "correspondence/api/v1/migration/correspondence/syncNotificationEvent";

    public SyncCorrespondenceNotificationEventTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SyncNotificationEvent_NewReminderNotificaiton_NewSaved()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no",NotificationChannelExt.Email, new DateTime(2024,1,7),false)
            .Build();
        
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
        Assert.NotNull(getCorrespondenceDetails.Notifications.Select(n => n.IsReminder == true));
    }

    [Fact]
    public async Task SyncNotificationEvent_ExistingNotification_NewNotSaved()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7), false)
            .Build();

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
                    Altinn2NotificationId =1,
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

    private async Task<Guid> MigrateCorrespondence(MigrateCorrespondenceExt migrateCorrespondenceExt)
    {
        var migrateResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
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
