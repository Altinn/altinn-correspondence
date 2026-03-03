using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class SyncCorrespondenceForwardingEventTests : MigrationTestBase
{
    internal const string syncCorrespondenceForwardingEventUrl = $"{migrateCorrespondenceControllerBaseUrl}/correspondence/syncForwardingEvent";

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
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
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
                   // Example of Copy sent to own email address
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToEmail = "user1@awesometestusers.com",
                   ForwardingText = "Keep this as a backup in my email."
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own digital mailbox
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
        var response = await _migrationClient.PostAsJsonAsync(syncCorrespondenceForwardingEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence and check that forwarding events are saved
        List<LegacyCorrespondenceHistoryExt>? legacyHistoryResponseContent = await GetLegacyHistory(correspondenceId, response);
        var forwardingEvents = legacyHistoryResponseContent.Where(h => h.ForwardingEvent != null).ToList();
        Assert.Equal(3, forwardingEvents.Count);
    }

    [Fact]
    public async Task SyncForwardingEvent_NewForwardingEvents_2ndHangfireEnqueueFails_FirstOk_2ndDatabaseRollback()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .Build();

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);
        Guid delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");

        // Arrange - Create a custom factory with a failing Hangfire mock
        var backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        // Mock the underlying Create method that Enqueue extension method calls internally
        backgroundJobClientMock
            .SetupSequence(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("Job-id-01")
            .Throws(new InvalidOperationException("Hangfire enqueue failed"));

        var customFactory = new CustomWebApplicationFactory
        {
            CustomServices = services =>
            {
                // Replace IBackgroundJobClient with our failing mock
                services.RemoveAll<IBackgroundJobClient>();
                services.AddSingleton(backgroundJobClientMock.Object);
            }
        };

        var customMigrationClient = customFactory.CreateClientWithAddedClaims(
            ("scope", AuthorizationConstants.MigrateScope));

        // Arrange sync call
        SyncCorrespondenceForwardingEventRequestExt request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>
            {
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own email address
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToEmail = "user1@awesometestusers.com",
                   ForwardingText = "Keep this as a backup in my email."
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own digital mailbox
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,5 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                }                
            }
        };

        // Act - Sync with custom factory - should fail due to Hangfire exception
        var response = await customMigrationClient.PostAsJsonAsync(syncCorrespondenceForwardingEventUrl, request);

        // Assert - Response indicates failure
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // Get updated details of the migrated correspondence and check that the first forwarding events is saved, but not the second due to transaction rollback from the exception
        List<LegacyCorrespondenceHistoryExt>? legacyHistoryResponseContent = await GetLegacyHistory(correspondenceId, response);        
        var historyWithforwardingEvent = legacyHistoryResponseContent.Where(h => h.ForwardingEvent != null).ToList();
        Assert.Equal(1, historyWithforwardingEvent.Count);
        Assert.Equal("user1@awesometestusers.com", historyWithforwardingEvent[0].ForwardingEvent.ForwardedToEmail);
    }

    [Fact]
    public async Task SyncForwardingEvent_NewForwardingEvents_Duplicates_NoNewSaved()
    {
        // Arrange
        Guid delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");

        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithForwardingEventHistory(new List<MigrateCorrespondenceForwardingEventExt>
                {
                    new MigrateCorrespondenceForwardingEventExt
                    {
                       // Example of Copy sent to own email address
                       ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                       ForwardedByPartyUuid = delegatedUserPartyUuid,
                       ForwardedByUserId = 123,
                       ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                       ForwardedToEmail = "user1@awesometestusers.com",
                       ForwardingText = "Keep this as a backup in my email."
                    },
                    new MigrateCorrespondenceForwardingEventExt
                    {
                       // Example of Copy sent to own digital mailbox
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
                })
            .Build();

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);        

        // Arrange sync call
        SyncCorrespondenceForwardingEventRequestExt request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>
            {
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own email address
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToEmail = "user1@awesometestusers.com",
                   ForwardingText = "Keep this as a backup in my email."
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own digital mailbox
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
        var response = await _migrationClient.PostAsJsonAsync(syncCorrespondenceForwardingEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence and check that forwarding events are saved
        List<LegacyCorrespondenceHistoryExt>? legacyHistoryResponseContent = await GetLegacyHistory(correspondenceId, response);
        var forwardingEvents = legacyHistoryResponseContent.Where(h => h.ForwardingEvent != null).ToList();
        Assert.Equal(3, forwardingEvents.Count);
    }

    [Fact]
    public async Task SyncForwardingEvent_NewForwardingEvents_OneDuplicateTwoNew_NewSaved()
    {
        // Arrange
        Guid delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");

        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithForwardingEventHistory(new List<MigrateCorrespondenceForwardingEventExt>
                {
                    new MigrateCorrespondenceForwardingEventExt
                    {
                       // Example of Copy sent to own email address
                       ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                       ForwardedByPartyUuid = delegatedUserPartyUuid,
                       ForwardedByUserId = 123,
                       ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                       ForwardedToEmail = "user1@awesometestusers.com",
                       ForwardingText = "Keep this as a backup in my email."
                    }
                })
            .Build();

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceForwardingEventRequestExt request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>
            {
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own email address
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                   ForwardedByPartyUuid = delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToEmail = "user1@awesometestusers.com",
                   ForwardingText = "Keep this as a backup in my email."
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sent to own digital mailbox
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
        var response = await _migrationClient.PostAsJsonAsync(syncCorrespondenceForwardingEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence and check that forwarding events are saved
        List<LegacyCorrespondenceHistoryExt>? legacyHistoryResponseContent = await GetLegacyHistory(correspondenceId, response);
        var forwardingEvents = legacyHistoryResponseContent.Where(h => h.ForwardingEvent != null).ToList();
        Assert.Equal(3, forwardingEvents.Count);
    }

    [Fact]
    public async Task SyncForwardingEvent_NoEventsSpecified_HttpBadRequest()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))            
            .Build();

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceForwardingEventRequestExt request = new SyncCorrespondenceForwardingEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceForwardingEventExt>()
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorrespondenceForwardingEventUrl, request);

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

    private async Task<List<LegacyCorrespondenceHistoryExt>?> GetLegacyHistory(Guid correspondenceId, HttpResponseMessage response)
    {
        var legacyHistoryResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/history");
        Assert.Equal(HttpStatusCode.OK, legacyHistoryResponse.StatusCode);
        var legacyHistoryRespondenseContent = await legacyHistoryResponse.Content.ReadFromJsonAsync<List<LegacyCorrespondenceHistoryExt>>(_responseSerializerOptions);
        Assert.NotNull(legacyHistoryRespondenseContent);
        return legacyHistoryRespondenseContent;
    }
}