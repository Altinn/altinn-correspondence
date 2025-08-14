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
public class SyncCorrespondenceForwardingEventTests : MigrationTestBase
{
    internal const string syncCorresponenceForwardingEventUrl = $"{migrateCorrespondenceControllerBaseUrl}/correspondence/syncForwardingEvent";

    public SyncCorrespondenceForwardingEventTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SyncForwardingEvent_NewForwardingEvents_NewSaved()
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
        Guid delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");

        // Arrange sync call
        SyncCorrespondenceForwardingEventRequestExt request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>
            {
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sendt to own email address
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToEmail = "user1@awesometestusers.com",
                   ForwardingText = "Keep this as a backup in my email."
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sendt to own digital mailbox
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,5 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Instance Delegation by User 1 to User2
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToUserId = 456,
                   ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                   ForwardingText = "User2, - look into this for me please. - User1.",
                   ForwardedToEmail = "user2@awesometestusers.com"
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceForwardingEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // TODO: How to validate as this is not available in the response?
    }

    [Fact]
    public async Task SyncForwardingEvent_NoEventsSpecified_HttpBadRequest()
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
        Guid delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");

        // Arrange sync call
        SyncCorrespondenceForwardingEventRequestExt request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>()
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceForwardingEventUrl, request);

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
}
