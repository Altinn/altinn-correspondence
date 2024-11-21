using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using ReverseMarkdown.Converters;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

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
        var basicCorrespondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
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
        var basicCorrespondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
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

    [Fact]
    public async Task InitializeMigrateAttachment_InitializeAndUpload()
    {
        InitializeAttachmentExt basicAttachment = new AttachmentBuilder().CreateAttachment().Build();

        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/attachment", basicAttachment);
        Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
        string attachmentIdstring = await initializeResponse.Content.ReadAsStringAsync();
        Guid attachmentId = Guid.Parse(attachmentIdstring);
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/migration/attachment/{attachmentId}/upload", content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_UploadBothAttachments_ThenInitializeCorrespondence()
    {

        InitializeAttachmentExt basicAttachment = new AttachmentBuilder().CreateAttachment().Build();

        var initializeResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/attachment", basicAttachment);
        string attachmentIdString = await initializeResponse.Content.ReadAsStringAsync();
        Guid attachmentId = Guid.Parse(attachmentIdString);
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        var uploadResponse = await _client.PostAsync($"correspondence/api/v1/migration/attachment/{attachmentId}/upload", content);


        InitializeAttachmentExt basicAttachment2 = new AttachmentBuilder().CreateAttachment().Build();

        var initializeResponse2 = await _client.PostAsJsonAsync("correspondence/api/v1/migration/attachment", basicAttachment2);
        string attachmentIdString2 = await initializeResponse2.Content.ReadAsStringAsync();
        Guid attachmentId2 = Guid.Parse(attachmentIdString2);
        byte[] file2 = Encoding.UTF8.GetBytes("Test av fil 2 opplasting");
        MemoryStream memoryStream2 = new(file2);
        StreamContent content2 = new(memoryStream2);
        var uploadResponse2 = await _client.PostAsync($"correspondence/api/v1/migration/attachment/{attachmentId2}/upload", content2);

        InitializeCorrespondencesExt initializeCorrespondencesExt = new CorrespondenceBuilder().CreateCorrespondence().WithExistingAttachments([attachmentId, attachmentId2]).Build();
        initializeCorrespondencesExt.Correspondence.SendersReference = "test 2024 10 09 09 45";
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            CorrespondenceData = initializeCorrespondencesExt,
            Altinn2CorrespondenceId = 12345,
            EventHistory = [ new CorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized, StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5))
            }
            ]
        };

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);

        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        Assert.True(uploadResponse2.IsSuccessStatusCode, uploadResponse2.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, initializeCorrespondenceResponse.ReasonPhrase + ":" + await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }
}
