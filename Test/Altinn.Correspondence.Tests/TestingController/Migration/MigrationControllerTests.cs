using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
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
public class MigrationControllerTests : MigrationTestBase
{
    const string makeAvailableUrl = "correspondence/api/v1/migration/makemigratedcorrespondenceavailable";
    const string migrateCorresponenceUrl = "correspondence/api/v1/migration/correspondence";
    public MigrationControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();

        SetNotificationHistory(migrateCorrespondenceExt);

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_GetCorrespondenceDetails_IncludesAltinn2Notifications()
    {
        // IsMigrating is set to false because we are not testing MakeAvailable, but we have to retrieve correspondence via GetcorrespondenceDetails to check Notifications.
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();

        SetNotificationHistory(migrateCorrespondenceExt);

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        var result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        // Act
        var getCorrespondenceDetailsResponse = await _migrationClient.GetAsync($"correspondence/api/v1/correspondence/{result.CorrespondenceId}/details");
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceDetailsResponse.StatusCode);
        Assert.Equal(8, response.Notifications.Count);
        Assert.Equal(migrateCorrespondenceExt.Altinn2CorrespondenceId, response.Altinn2CorrespondenceId);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_GetCorrespondenceLegacy_IncludesAltinn2Notifications()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        var result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        // Act
        var getCorrespondenceDetailsResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{result.CorrespondenceId}/history");
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<List<LegacyCorrespondenceHistoryExt>>(_responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceDetailsResponse.StatusCode);
        Assert.Equal(8, response.Where(r => r.Notification != null).Count());
        Assert.Equal(4, response.Where(r => r.Notification != null && r.StatusChanged == migrateCorrespondenceExt.NotificationHistory.First().NotificationSent).Count());
        Assert.Equal(4, response.Where(r => r.Notification != null && r.StatusChanged == migrateCorrespondenceExt.NotificationHistory.Last().NotificationSent).Count());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_GetCorrespondenceLegacy_GetsMessageSenderAsCreatingUserName()
    {
        const string messageSender = "Test MessageSender";
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithMessageSender(messageSender)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6), _testUserPartyUuId)
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7), _testUserPartyUuId)
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        var result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        // Act
        var getCorrespondenceDetailsResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{result.CorrespondenceId}/history");
        var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<List<LegacyCorrespondenceHistoryExt>>(_responseSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getCorrespondenceDetailsResponse.StatusCode);
        Assert.Equal(8, response.Where(r => r.Notification != null).Count());
        Assert.Equal(4, response.Where(r => r.Notification != null && r.StatusChanged == migrateCorrespondenceExt.NotificationHistory.First().NotificationSent).Count());
        Assert.Equal(4, response.Where(r => r.Notification != null && r.StatusChanged == migrateCorrespondenceExt.NotificationHistory.Last().NotificationSent).Count());
        Assert.Equal(messageSender, response.First(r => r.Status == "Published").User.Name);
        Assert.Equal(_delegatedUserName, response.First(r => r.Status == "Archived").User.Name);
        Assert.Equal(_delegatedUserName, response.First(r => r.Status == "Read").User.Name);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_WithForwarded()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();

        SetNotificationHistory(migrateCorrespondenceExt);

        migrateCorrespondenceExt.ForwardingHistory = new List<MigrateCorrespondenceForwardingEventExt>
        {
            new MigrateCorrespondenceForwardingEventExt
            {
               // Example of Copy sendt to own email address
               ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
               ForwardedByPartyUuid = new Guid("25C6E04A-4A1E-4122-A929-0168255B7E99"),
               ForwardedByUserId = 123,
               ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
               ForwardedToEmail = "user1@awesometestusers.com",
               ForwardingText = "Keep this as a backup in my email."
            },
            new MigrateCorrespondenceForwardingEventExt
            {
               // Example of Copy sendt to own digital mailbox
               ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,5 ,0)),
               ForwardedByPartyUuid = new Guid("25C6E04A-4A1E-4122-A929-0168255B7E99"),
               ForwardedByUserId = 123,
               ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
               MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
            },
            new MigrateCorrespondenceForwardingEventExt
            {
               // Example of Instance Delegation by User 2 to User 3
               ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15 ,0)),
               ForwardedByPartyUuid = new Guid("966A0220-1332-43C4-A405-4C1060B213E7"),
               ForwardedByUserId = 222,
               ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
               ForwardedToUserId = 456,
               ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
               ForwardingText = "User3, - please look into this for me please. - User2",
               ForwardedToEmail = "user3@awesometestusers.com"
            }
        };

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_NotReadNoNotifications()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
             .CreateMigrateCorrespondence()
             .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateAttachment_InitializeAndUpload()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        using MemoryStream memoryStream = new(file);
        using StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _migrationClient.PostAsync(command, content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateAttachment_DuplicateAltinn2AttachmentId_SecondRequestReturnsFirstAttachmentId()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        migrateAttachmentExt.Altinn2AttachmentId = "SS" + (new Random()).Next();
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        using MemoryStream memoryStream = new(file);
        using StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _migrationClient.PostAsync(command, content);
        string attachment1Id = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        var uploadResponse2 = await _migrationClient.PostAsync(command, content);
        string attachment2Id = await uploadResponse2.Content.ReadAsStringAsync();
        Assert.True(uploadResponse2.StatusCode == System.Net.HttpStatusCode.OK, uploadResponse2.ReasonPhrase + ":" + await uploadResponse2.Content.ReadAsStringAsync());
        Assert.Equal(attachment1Id, attachment2Id);
    }

    [Fact]
    public async Task MakeCorrespondenceAvailable()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6, 11, 10, 21))
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 7, 15, 11, 56))
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 8, 14, 19, 22))
            .WithStatusEvent(CorrespondenceStatusExt.Confirmed, new DateTime(2024, 1, 8, 14, 20, 5))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 9, 10, 50, 17))
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);

        CorrespondenceMigrationStatusExt resultObj = await MigrateSingleCorrespondence(migrateCorrespondenceExt);
        Assert.NotNull(resultObj);

        MakeCorrespondenceAvailableRequestExt request = new MakeCorrespondenceAvailableRequestExt()
        {
            CreateEvents = false,
            CorrespondenceIds = [resultObj.CorrespondenceId],
            CorrespondenceId = resultObj.CorrespondenceId
        };
        var makeAvailableResponse = await _migrationClient.PostAsJsonAsync(makeAvailableUrl, request);
        MakeCorrespondenceAvailableResponseExt respExt = await makeAvailableResponse.Content.ReadFromJsonAsync<MakeCorrespondenceAvailableResponseExt>();

        // Verify that correspondence has IsMigrating set to false, which means we can retrieve it through GetOverview.
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{resultObj.CorrespondenceId}/content");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode);

        Assert.True(makeAvailableResponse.IsSuccessStatusCode);
        Assert.NotNull(respExt.Statuses);
        Assert.Equal(1, respExt.Statuses.Count);
        Assert.True(respExt.Statuses.First().Ok);
    }

    [Fact]
    public async Task MakeCorrespondenceAvailable_Defined()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6, 11, 10, 21))
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 7, 15, 11, 56))
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 8, 14, 19, 22))
            .WithStatusEvent(CorrespondenceStatusExt.Confirmed, new DateTime(2024, 1, 8, 14, 20, 5))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 9, 10, 50, 17))
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);
        string jsonCorr = "{\"correspondenceData\":{\"correspondence\":{\"resourceId\":\"skd-migratedcorrespondence-3476-130314\",\"sender\":\"urn:altinn:organization:identifier-no:974761076\",\"senderPartyUuid\":\"e6e80419-0019-4892-b8a7-78ac03eb3c51\",\"sendersReference\":\"b4199d2c-c063-4bb2-bce6-0b4d69f5acea\",\"messageSender\":\"Skatteetaten\",\"content\":{\"language\":\"nb\",\"messageTitle\":\"A03 a-melding tilbakemelding for 2025-05 - meldingsId: tlx-1232714052\",\"messageSummary\":\"\",\"messageBody\":\"Tilbakemelding på a-melding\"},\"requestedPublishTime\":\"2025-06-30T12:06:09.487+02:00\",\"allowSystemDeleteAfter\":null,\"dueDateTime\":null,\"externalReferences\":[],\"propertyList\":{\"Altinn2ArchiveUnitReference\":\"AR20699019\"},\"replyOptions\":[],\"ignoreReservation\":null,\"published\":\"2025-06-30T12:06:09.487+02:00\",\"isConfirmationNeeded\":false},\"recipients\":[\"urn:altinn:organization:identifier-no:313414450\"],\"recipientPartyUuids\":[\"aa0c3933-4d4e-4bbb-8ac6-1becd706ffe8\"],\"existingAttachments\":[]},\"altinn2CorrespondenceId\":30088189,\"eventHistory\":[{\"status\":0,\"statusText\":\"Correspondence Created in Altinn 2\",\"statusChanged\":\"2025-06-30T12:06:09.487+02:00\",\"eventUserUuid\":\"00000000-0000-0000-0000-000000000000\",\"eventUserPartyUuid\":\"00000000-0000-0000-0000-000000000000\"},{\"status\":2,\"statusText\":\"Correspondence Published in Altinn 2\",\"statusChanged\":\"2025-06-30T12:06:09.487+02:00\",\"eventUserUuid\":\"e6e80419-0019-4892-b8a7-78ac03eb3c51\",\"eventUserPartyUuid\":\"e6e80419-0019-4892-b8a7-78ac03eb3c51\"},{\"status\":4,\"statusText\":\"Migrated event Read from Altinn 2\",\"statusChanged\":\"2025-06-30T12:06:46.337+02:00\",\"eventUserUuid\":\"2a064dc8-193e-4ca0-9027-9aef49c96db1\",\"eventUserPartyUuid\":\"2a064dc8-193e-4ca0-9027-9aef49c96db1\"}],\"notificationHistory\":[],\"forwardingHistory\":[],\"IsMigrating\":true,\"created\":\"2025-06-30T12:06:09.487+02:00\",\"partyId\":51843981}";

        migrateCorrespondenceExt = JsonConvert.DeserializeObject<MigrateCorrespondenceExt>(jsonCorr);
        CorrespondenceMigrationStatusExt resultObj = await MigrateSingleCorrespondence_NoAdd(migrateCorrespondenceExt);
        Assert.NotNull(resultObj);

        MakeCorrespondenceAvailableRequestExt request = new MakeCorrespondenceAvailableRequestExt()
        {
            CreateEvents = false,
            CorrespondenceIds = [resultObj.CorrespondenceId],
            CorrespondenceId = resultObj.CorrespondenceId
        };
        var makeAvailableResponse = await _migrationClient.PostAsJsonAsync(makeAvailableUrl, request);
        MakeCorrespondenceAvailableResponseExt respExt = await makeAvailableResponse.Content.ReadFromJsonAsync<MakeCorrespondenceAvailableResponseExt>();

        // Verify that correspondence has IsMigrating set to false, which means we can retrieve it through GetOverview.
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{resultObj.CorrespondenceId}/content");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode);

        Assert.True(makeAvailableResponse.IsSuccessStatusCode);
        Assert.NotNull(respExt.Statuses);
        Assert.Equal(1, respExt.Statuses.Count);
        Assert.True(respExt.Statuses.First().Ok);
    }

    [Fact]
    public async Task MakeCorrespondenceAvailable_DueAtInThePastNoSummary()
    {
        // In this case, summary and DueAt.
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6, 11, 10, 21))
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 7, 15, 11, 56))
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 8, 14, 19, 22))
            .WithStatusEvent(CorrespondenceStatusExt.Confirmed, new DateTime(2024, 1, 8, 14, 20, 5))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 9, 10, 50, 17))
            .WithDueAt(new DateTime(2025, 05, 01))
            .WithSummary("")
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);

        CorrespondenceMigrationStatusExt resultObj = await MigrateSingleCorrespondence(migrateCorrespondenceExt);
        Assert.NotNull(resultObj);

        MakeCorrespondenceAvailableRequestExt request = new MakeCorrespondenceAvailableRequestExt()
        {
            CreateEvents = false,
            CorrespondenceIds = [resultObj.CorrespondenceId],
            CorrespondenceId = resultObj.CorrespondenceId
        };
        var makeAvailableResponse = await _migrationClient.PostAsJsonAsync(makeAvailableUrl, request);
        MakeCorrespondenceAvailableResponseExt respExt = await makeAvailableResponse.Content.ReadFromJsonAsync<MakeCorrespondenceAvailableResponseExt>();

        // Verify that correspondence has IsMigrating set to false, which means we can retrieve it through GetOverview.
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{resultObj.CorrespondenceId}/content");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode);

        Assert.True(makeAvailableResponse.IsSuccessStatusCode);
        Assert.NotNull(respExt.Statuses);
        Assert.Equal(1, respExt.Statuses.Count);
        Assert.True(respExt.Statuses.First().Ok);
    }

    [Fact]
    public async Task MakeCorrespondenceAvailable_OnCall()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 12, 14, 20, 11))
            .WithStatusEvent(CorrespondenceStatusExt.Confirmed, new DateTime(2024, 1, 12, 14, 21, 05))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 22, 09, 55, 20))
            .WithCreatedAt(new DateTime(2024, 1, 1, 03, 09, 21))
            .WithRecipient("urn:altinn:person:identifier-no:29909898925")
            .WithResourceId("skd-migratedcorrespondence-5229-1")
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);

        migrateCorrespondenceExt.MakeAvailable = true;

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
        CorrespondenceMigrationStatusExt resultObj = JsonConvert.DeserializeObject<CorrespondenceMigrationStatusExt>(result);
        Assert.NotNull(resultObj.DialogId);
        
        // Verify that correspondence has IsMigrating set to false, which means we can retrieve it through GetOverview.
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{resultObj.CorrespondenceId}/content");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task MakeCorrespondenceAvailable_Multiple()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();
        SetNotificationHistory(migrateCorrespondenceExt);

        MakeCorrespondenceAvailableRequestExt request = new MakeCorrespondenceAvailableRequestExt()
        {
            CreateEvents = false,
            CorrespondenceIds = new(),
            CorrespondenceId = null
        };

        var resultObj = await MigrateSingleCorrespondence(migrateCorrespondenceExt);
        Assert.NotNull(resultObj);

        request.CorrespondenceIds.Add(resultObj.CorrespondenceId);
        resultObj = await MigrateSingleCorrespondence(migrateCorrespondenceExt);
        Assert.NotNull(resultObj);

        request.CorrespondenceIds.Add(resultObj.CorrespondenceId);

        migrateCorrespondenceExt.Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId + 1;
        resultObj = await MigrateSingleCorrespondence(migrateCorrespondenceExt);
        Assert.NotNull(resultObj);

        request.CorrespondenceIds.Add(resultObj.CorrespondenceId);

        var makeAvailableResponse = await _migrationClient.PostAsJsonAsync(makeAvailableUrl, request);
        MakeCorrespondenceAvailableResponseExt respExt = await makeAvailableResponse.Content.ReadFromJsonAsync<MakeCorrespondenceAvailableResponseExt>();

        Assert.True(makeAvailableResponse.IsSuccessStatusCode);
        Assert.NotNull(respExt.Statuses);
        Assert.Equal(3, respExt.Statuses.Count);
        Assert.True(respExt.Statuses.First().Ok);
        Assert.True(respExt.Statuses.Last().Ok);
        Assert.NotSame(respExt.Statuses.First().CorrespondenceId, respExt.Statuses.Last().CorrespondenceId);
        
        // Verify that correspondence has IsMigrating set to false, which means we can retrieve it through GetOverview.
        var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{respExt.Statuses.Last().CorrespondenceId}/content");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode);

        // Verify that correspondence has IsMigrating set to false, which means we can retrieve it through GetOverview.
        getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{respExt.Statuses.First().CorrespondenceId}/content");
        Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode);
    }

    private async Task<CorrespondenceMigrationStatusExt> MigrateSingleCorrespondence(MigrateCorrespondenceExt migrateCorrespondenceExt)
    {
        migrateCorrespondenceExt.Altinn2CorrespondenceId = migrateCorrespondenceExt.Altinn2CorrespondenceId + 1;
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode);
        CorrespondenceMigrationStatusExt resultObj = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>();
        return resultObj;
    }


    private async Task<CorrespondenceMigrationStatusExt> MigrateSingleCorrespondence_NoAdd(MigrateCorrespondenceExt migrateCorrespondenceExt)
    {
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode);
        CorrespondenceMigrationStatusExt resultObj = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>();
        return resultObj;
    }

    private string GetAttachmentCommand(MigrateInitializeAttachmentExt attachment)
    {
        return $"correspondence/api/v1/migration/attachment" +
            $"?resourceId={HttpUtility.UrlEncode(attachment.ResourceId)}" +
            $"&senderPartyUuid={HttpUtility.UrlEncode(attachment.SenderPartyUuid.ToString())}" +
            $"&sendersReference={HttpUtility.UrlEncode(attachment.SendersReference)}" +
            $"&displayName={HttpUtility.UrlEncode(attachment.DisplayName)}" +
            $"&isEncrypted={HttpUtility.UrlEncode(attachment.IsEncrypted.ToString())}" +
            $"&fileName={HttpUtility.UrlEncode(attachment.FileName)}" +
            $"&sender={HttpUtility.UrlEncode(attachment.Sender)}" +
            $"&altinn2AttachmentId={HttpUtility.UrlEncode(attachment.Altinn2AttachmentId.ToString())}";
    }

    [Fact]
    public async Task InitializeMigrateAttachment_InitializeAndUpload_NewUploadEndpoint()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();

        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        using MemoryStream memoryStream = new(file);
        using StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _migrationClient.PostAsync(command, content);

        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_UploadBothAttachments_ThenInitializeCorrespondence()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        using MemoryStream memoryStream = new(file);
        using StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _migrationClient.PostAsync(command, content);
        Guid attachmentId = Guid.Parse(uploadResponse.Content.ReadAsStringAsync().Result.Trim('"'));

        MigrateInitializeAttachmentExt migrateAttachmentExt2 = new MigrateAttachmentBuilder().CreateAttachment().Build();
        byte[] file2 = Encoding.UTF8.GetBytes("Test av fil 2 opplasting");
        using MemoryStream memoryStream2 = new(file2);
        using StreamContent content2 = new(memoryStream2);
        string command2 = GetAttachmentCommand(migrateAttachmentExt2);
        var uploadResponse2 = await _migrationClient.PostAsync(command2, content);
        Guid attachmentId2 = Guid.Parse(uploadResponse2.Content.ReadAsStringAsync().Result.Trim('"'));

        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithSendersReference("test 2024 10 09 09 45")
            .WithExistingAttachments([attachmentId, attachmentId2])
            .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);

        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        Assert.True(uploadResponse2.IsSuccessStatusCode, uploadResponse2.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, initializeCorrespondenceResponse.ReasonPhrase + ":" + await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_IsMigrateTrue_SetsFlagToTrue()
    {
        // Arrange
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .Build();

        // Act
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync(migrateCorresponenceUrl, migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);
        var scope = _factory.Services.CreateScope();
        var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
        CorrespondenceEntity? correspondence = await correspondenceRepository.GetCorrespondenceById(result.CorrespondenceId, true, true, false, CancellationToken.None, true);

        // Assert
        Assert.NotNull(correspondence);
        Assert.True(correspondence.IsMigrating);
    }
    
    private static void SetNotificationHistory(MigrateCorrespondenceExt migrateCorrespondenceExt)
    {
        migrateCorrespondenceExt.NotificationHistory =
        [
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 1,
                NotificationAddress = "testemail@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 2,
                NotificationAddress = "testemail2@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 3,
                NotificationAddress = "testemail3@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 4,
                NotificationAddress = "testemail4@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 5,
                NotificationAddress = "123456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 11)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 6,
                NotificationAddress = "223456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 11)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 7,
                NotificationAddress = "323456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 11)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 754537533,
                NotificationAddress = "423456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 11)),
                IsReminder = false
            }
        ];
    }
}
