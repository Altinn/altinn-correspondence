using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Markdig;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class MigrationControllerTests : IClassFixture<MaskinportenWebApplicationFactory>
{
    private readonly MaskinportenWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public MigrationControllerTests(MaskinportenWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClientWithAddedClaims(("scope", "altinn:correspondence.migrate"));
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence()
    {
        InitializeCorrespondencesExt basicCorrespondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            CorrespondenceData = basicCorrespondence,
            Altinn2CorrespondenceId = 12345,
            EventHistory =
        [
            new CorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5))
            },
            new CorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Read,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6))
            },
            new CorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Archived,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7))
            }
        ]
        };

        migrateCorrespondenceExt.NotificationHistory =
        [
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 1,
                NotificationAddress = "testemail@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 2,
                NotificationAddress = "testemail2@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 3,
                NotificationAddress = "testemail3@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 4,
                NotificationAddress = "testemail4@altinn.no",
                NotificationChannel = NotificationChannelExt.Email,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 5,
                NotificationAddress = "123456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 6,
                NotificationAddress = "223456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 7,
                NotificationAddress = "323456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 754537533,
                NotificationAddress = "423456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04))
            }
        ];

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_NotReadNoNotifications()
    {
        InitializeCorrespondencesExt basicCorrespondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            CorrespondenceData = basicCorrespondence,
            Altinn2CorrespondenceId = 12345,
            EventHistory =
        [
            new CorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5))
            }
        ]
        };

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    private async Task<HttpResponseMessage> UploadAttachment(string? attachmentId, ByteArrayContent? originalAttachmentData = null)
    {
        if (attachmentId == null)
        {
            Assert.Fail("AttachmentId is null");
        }
        var data = originalAttachmentData ?? new ByteArrayContent(new byte[] { 1, 2, 3, 4 });

        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/attachment/{attachmentId}/upload", data);
        return uploadResponse;
    }
    private async Task<AttachmentOverviewExt?> InitializeAttachment()
    {
        var attachment = InitializeAttachmentFactory.BasicAttachment();
        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
        initializeResponse.EnsureSuccessStatusCode();
        var attachmentId = await initializeResponse.Content.ReadAsStringAsync();
        var overview = await (await UploadAttachment(attachmentId)).Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);
        return overview;
    }
}