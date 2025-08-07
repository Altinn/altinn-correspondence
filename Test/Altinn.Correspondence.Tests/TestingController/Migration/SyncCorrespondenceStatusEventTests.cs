using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
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
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Web;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class SyncCorrespondenceStatusEventTests : MigrationTestBase
{
    internal const string syncCorresponenceStatusEventUrl = "correspondence/api/v1/migration/correspondence/syncStatusEvent";

    public SyncCorrespondenceStatusEventTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SyncCorrespondenceStatusEvent_ReadAndConfirmedByOtherUser()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithNotificationHistoryEvent(1, "testemail@altinn.no",NotificationChannelExt.Email, new DateTime(2024,1,7),false)
            .Build();
        
        // Setup initial migrated
        var migrateCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        Assert.True(migrateCorrespondenceResponse.IsSuccessStatusCode);
        CorrespondenceMigrationStatusExt resultObj = await migrateCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>();
        var correspondenceId = resultObj.CorrespondenceId;

        // Arrange sync call
        SyncCorrespondenceStatusEventRequestExt syncCorrespondenceStatusEventRequestExt = new SyncCorrespondenceStatusEventRequestExt
        {
            CorrespondenceId = correspondenceId,
            SyncedEvents = new List<MigrateCorrespondenceStatusEventExt>
            {
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.Read,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7, 12, 0,0)),
                    EventUserUuid = new Guid("645AC4DC-6F05-47AB-9FF4-AC6A13B0C44E"),
                    EventUserPartyUuid = new Guid("02DC1186-7DF2-4116-B8C6-4C2D1CAAC662"),
                },
                new MigrateCorrespondenceStatusEventExt
                {
                    Status = CorrespondenceStatusExt.Confirmed,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7, 12, 1, 0)),
                    EventUserUuid = new Guid("645AC4DC-6F05-47AB-9FF4-AC6A13B0C44E"),
                    EventUserPartyUuid = new Guid("02DC1186-7DF2-4116-B8C6-4C2D1CAAC662"),
                }
            }
        };

        // Act
        var syncCorrespondenceStatusEventResponse = await _migrationClient.PostAsJsonAsync(syncCorresponenceStatusEventUrl, syncCorrespondenceStatusEventRequestExt);

        // Assert
        Assert.True(syncCorrespondenceStatusEventResponse.IsSuccessStatusCode);

        // TODO: Expand with GetDetails and verification of the status events stored corrrectly
    }
}
