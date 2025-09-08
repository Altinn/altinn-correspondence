using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class SyncCorrespondenceStatusEventTests : MigrationTestBase
{
    internal const string syncCorresponenceStatusEventUrl = $"{migrateCorrespondenceControllerBaseUrl}/correspondence/syncStatusEvent";

    private readonly Guid _defaultUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
    private readonly Guid _defaultUserUuid = new Guid("2607D808-29EC-4BD8-B89F-B9D14BDE634C");
    private readonly Guid _secondUserPartyUuid = new Guid("AE985685-5D8F-45E0-AE00-240F5F5C60C5");
    private readonly Guid _secondUserUuid = new Guid("AE985685-5D8F-45E0-AE00-240F5F5C60C5");

    public SyncCorrespondenceStatusEventTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_ReadAndConfirmedByOtherUser__NewStatusesSaved()
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
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.Read,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                    EventUserPartyUuid = _secondUserPartyUuid,
                    EventUserUuid = _secondUserUuid
                },
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.Confirmed,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7)),
                    EventUserPartyUuid = _secondUserPartyUuid,
                    EventUserUuid = _secondUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);
        // Assert that the new statuses are saved
        AssertStatusEventSet(request.SyncedEvents[0], getCorrespondenceDetails);
        AssertStatusEventSet(request.SyncedEvents[1], getCorrespondenceDetails);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_DuplicateEvent__NewStatusNotSaved()
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
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.Read,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6, 0, 0, 0, 250)), // within 1 second of the original event
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);
        // Assert that the new statuses are not saved
        AssertStatusEventNotSet(request.SyncedEvents[0], getCorrespondenceDetails);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_Archived__NewStatuseSaved()
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
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.Archived,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Assert that the new statuses are saved
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);        
        AssertStatusEventSet(request.SyncedEvents[0], getCorrespondenceDetails);

        // How to verify that the Dialog porten dialog is updated? - Done in Handler tests.
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedByRecipient__CorrespondencePurged()
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
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.PurgedByRecipient,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Assert that the Correspondence is purged by getting NOT FOUND
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedRecipient_NotAvailableInlegacy()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, DateTime.Now)
            .WithCreatedAt(DateTime.Now)
            .WithRecipient("urn:altinn:organization:identifier-no:991825827")
            .WithResourceId("skd-migratedcorrespondence-5229-1")
            .Build();
        migrateCorrespondenceExt.MakeAvailable = true;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.PurgedByRecipient,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Assert that the Correspondence is not available and purged
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);

        var listPayload = new LegacyGetCorrespondencesRequestExt
        {
            InstanceOwnerPartyIdList = new int[] { },
            IncludeActive = false,
            IncludeArchived = false,
            IncludeDeleted = true,
            FilterMigrated = false,
            From = DateTimeOffset.UtcNow.AddDays(-5),
            To = DateTimeOffset.UtcNow.AddDays(5)
        };
        var correspondenceList = await _legacyClient.PostAsJsonAsync($"correspondence/api/v1/legacy/correspondence", listPayload);
        var correspondenceListResponse = await correspondenceList.Content.ReadFromJsonAsync<LegacyGetCorrespondencesResponse>(_responseSerializerOptions);
        Assert.DoesNotContain(correspondenceListResponse?.Items, c => c.CorrespondenceId == correspondenceId);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedByAltinn__CorrespondencePurged()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(MigrateCorrespondenceStatusExt.Read, new DateTime(2024, 1, 12, 14, 20, 11))
            .WithCreatedAt(new DateTime(2024, 1, 1, 03, 09, 21))
            .WithRecipient("urn:altinn:person:identifier-no:29909898925")
            .WithResourceId("skd-migratedcorrespondence-5229-1")
            .Build();
        migrateCorrespondenceExt.MakeAvailable = true;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.PurgedByAltinn,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Assert that the Correspondence is purged by getting NOT FOUND
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_NoEventsSpecified__HttpBadRequest()
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
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>()
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedByRecipient_MakeAvailableFails()
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
        migrateCorrespondenceExt.MakeAvailable = false;

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceStatusEventRequestExt request = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = MigrateCorrespondenceStatusExt.PurgedByRecipient,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var response = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Assert that the Correspondence is purged by getting NOT FOUND
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);

        // Verify that making the correspondence available again fails as it is purged
        MakeCorrespondenceAvailableRequestExt makeAvailableRequest = new MakeCorrespondenceAvailableRequestExt()
        {
            CreateEvents = true,
            CorrespondenceId = correspondenceId
        };
        var makeAvailableResponse = await _migrationClient.PostAsJsonAsync(makeAvailableUrl, makeAvailableRequest);
        Assert.True(makeAvailableResponse.IsSuccessStatusCode);
        MakeCorrespondenceAvailableResponseExt respExt = await makeAvailableResponse.Content.ReadFromJsonAsync<MakeCorrespondenceAvailableResponseExt>();
        Assert.NotNull(respExt.Statuses[0].Error);
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

    private void AssertStatusEventSet(MigrateCorrespondenceStatusEventExt expected, CorrespondenceDetailsExt detailsExt)
    {
        var statusEvent = detailsExt.StatusHistory.FirstOrDefault(x => x.Status == MapStatusFromMigrate(expected.Status) &&
                                                                      x.StatusChanged.Equals(expected.StatusChanged));
        Assert.NotNull(statusEvent);      
    }

    private void AssertStatusEventNotSet(MigrateCorrespondenceStatusEventExt expected, CorrespondenceDetailsExt detailsExt)
    {
        var statusEvent = detailsExt.StatusHistory.FirstOrDefault(x => x.Status == MapStatusFromMigrate(expected.Status) &&
                                                                      x.StatusChanged.Equals(expected.StatusChanged));
        Assert.Null(statusEvent);
    }

    private CorrespondenceStatusExt MapStatusFromMigrate(MigrateCorrespondenceStatusExt status)
    {
        return status switch
        {   
            MigrateCorrespondenceStatusExt.Read => CorrespondenceStatusExt.Read,
            MigrateCorrespondenceStatusExt.Confirmed => CorrespondenceStatusExt.Confirmed,
            MigrateCorrespondenceStatusExt.Archived => CorrespondenceStatusExt.Archived,
            MigrateCorrespondenceStatusExt.PurgedByRecipient => CorrespondenceStatusExt.PurgedByRecipient,
            MigrateCorrespondenceStatusExt.PurgedByAltinn => CorrespondenceStatusExt.PurgedByAltinn,
            _ => throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}"),
        };
    }
}
