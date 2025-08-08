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
public class SyncCorrespondenceStatusEventTests : MigrationTestBase
{
    internal const string syncCorresponenceStatusEventUrl = "correspondence/api/v1/migration/correspondence/syncStatusEvent";
    private Guid _defaultUserPartyUuid = new Guid("11112222333344445555666677778888");
    private Guid _defaultUserUuid = new Guid("2607D808-29EC-4BD8-B89F-B9D14BDE634C");

    private Guid _secondUserPartyUuid = new Guid("AE985685-5D8F-45E0-AE00-240F5F5C60C5");
    private Guid _secondUserUuid = new Guid("AE985685-5D8F-45E0-AE00-240F5F5C60C5");

    public SyncCorrespondenceStatusEventTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_ReadAndConfirmedByOtherUser__NewStatusesSaved()
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
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.Read,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                    EventUserPartyUuid = _secondUserPartyUuid,
                    EventUserUuid = _secondUserUuid
                },
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.Confirmed,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7)),
                    EventUserPartyUuid = _secondUserPartyUuid,
                    EventUserUuid = _secondUserUuid
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);
        // Assert that the new statuses are saved
        AssertStatusEventSet(syncCorrespondenceStatusEventRequestExt.SyncedEvents[0], getCorrespondenceDetails);
        AssertStatusEventSet(syncCorrespondenceStatusEventRequestExt.SyncedEvents[1], getCorrespondenceDetails);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_DuplicateEvent__NewStatusNotSaved()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6, 0, 0, 0))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7), false)
            .Build();

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.Read,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6, 0, 0, 0, 250)), // within 1 second of the original event
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);

        // Get updated details of the migrated correspondence
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);
        // Assert that the new statuses are not saved
        AssertStatusEventNotSet(syncCorrespondenceStatusEventRequestExt.SyncedEvents[0], getCorrespondenceDetails);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_Archived__NewStatuseSaved()
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
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.Archived,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);
        // Assert that the new statuses are saved
        var getCorrespondenceDetails = await GetCorrespondenceDetailsAsync(correspondenceId);        
        AssertStatusEventSet(syncCorrespondenceStatusEventRequestExt.SyncedEvents[0], getCorrespondenceDetails);

        // How to verify that the correspondence is archived?
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedByRecipient__CorrespondencePurged()
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
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.PurgedByRecipient,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);
        // Assert that the Correspondence is purged by getting NOT FOUND
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedByAltinn__CorrespondencePurged()
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
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.PurgedByAltinn,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);
        // Assert that the Correspondence is purged by getting NOT FOUND
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_PurgedByAltinn_AlreadyPurged__OK()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.PurgedByRecipient, new DateTime(2024, 1, 8))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no", NotificationChannelExt.Email, new DateTime(2024, 1, 7), false)
            .Build();

        // Setup initial Migrated Correspondence
        var correspondenceId = await MigrateCorrespondence(migrateCorrespondenceExt);

        // Arrange sync call
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.PurgedByAltinn,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 8)),
                    EventUserPartyUuid = _defaultUserPartyUuid,
                    EventUserUuid = _defaultUserUuid
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);
        // Assert that the Correspondence is purged by getting NOT FOUND
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
        Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
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

    private void AssertStatusEventSet(MigrateCorrespondenceStatusEventExt expected, CorrespondenceDetailsExt detailsExt)
    {
        var statusEvent = detailsExt.StatusHistory.FirstOrDefault(x => x.Status == expected.Status &&
                                                                      x.StatusChanged.Equals(expected.StatusChanged));
        Assert.NotNull(statusEvent);
        Assert.Equal(expected.Status, statusEvent.Status);
        Assert.Equal(expected.StatusChanged, statusEvent.StatusChanged);        
    }

    private void AssertStatusEventNotSet(MigrateCorrespondenceStatusEventExt expected, CorrespondenceDetailsExt detailsExt)
    {
        var statusEvent = detailsExt.StatusHistory.FirstOrDefault(x => x.Status == expected.Status &&
                                                                      x.StatusChanged.Equals(expected.StatusChanged));
        Assert.Null(statusEvent);
    }
}
