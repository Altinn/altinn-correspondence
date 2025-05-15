using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Web;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class MigrationControllerTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public MigrationControllerTests(CustomWebApplicationFactory factory)
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
        Guid userPartyGuid = new Guid("11112222333344445555666677778888");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        basicCorrespondence.Correspondence.Content.MessageBody = "<html><header>test header</header><body>test body</body></html>";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            Created = new DateTimeOffset(new DateTime(2024, 1, 5)),
            CorrespondenceData = basicCorrespondence,
            Altinn2CorrespondenceId = 12345,
            EventHistory =
        [
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                EventUserPartyUuid = userPartyGuid
            },
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Read,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                EventUserPartyUuid = userPartyGuid
            },
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Archived,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7)),
                EventUserPartyUuid = userPartyGuid
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
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 6,
                NotificationAddress = "223456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 7,
                NotificationAddress = "323456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 754537533,
                NotificationAddress = "423456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            }
        ];

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_WithForwarded()
    {
        var basicCorrespondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        basicCorrespondence.Correspondence.Content.MessageBody = "<html><header>test header</header><body>test body</body></html>";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            Created = new DateTimeOffset(new DateTime(2024, 1, 5)),
            CorrespondenceData = basicCorrespondence,
            Altinn2CorrespondenceId = 12345,
            EventHistory =
        [
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                EventUserPartyUuid = Guid.NewGuid()
            },
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Read,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                EventUserPartyUuid = Guid.NewGuid()
            },
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Archived,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 7)),
                EventUserPartyUuid = Guid.NewGuid()
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
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 6,
                NotificationAddress = "223456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 7,
                NotificationAddress = "323456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            },
            new MigrateCorrespondenceNotificationExt()
            {
                Altinn2NotificationId = 754537533,
                NotificationAddress = "423456789",
                NotificationChannel = NotificationChannelExt.Sms,
                NotificationSent = new DateTimeOffset(new DateTime(2024, 01, 04)),
                IsReminder = false
            }
        ];

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
        Guid userPartyGuid = new Guid("11112222333344445555666677778888");
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            Created = new DateTimeOffset(new DateTime(2024, 1, 5)),
            CorrespondenceData = basicCorrespondence,
            Altinn2CorrespondenceId = 12345,
            EventHistory =
        [
            new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized,
                StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                EventUserPartyUuid = userPartyGuid
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
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _client.PostAsync(command, content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateAttachment_DuplicateAltinn2AttachmentId_FailsWithConflict()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        migrateAttachmentExt.Altinn2AttachmentId = (new Random()).Next().ToString();
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _client.PostAsync(command, content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        var uploadResponse2 = await _client.PostAsync(command, content);
        Assert.True(uploadResponse2.StatusCode == System.Net.HttpStatusCode.Conflict, uploadResponse2.ReasonPhrase + ":" + await uploadResponse2.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateAttachment_TwoInARowWithoutAltinn2AttachmentId_Succeeds()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        migrateAttachmentExt.Altinn2AttachmentId = null;
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _client.PostAsync(command, content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        var uploadResponse2 = await _client.PostAsync(command, content);
        Assert.True(uploadResponse2.IsSuccessStatusCode, uploadResponse2.ReasonPhrase + ":" + await uploadResponse2.Content.ReadAsStringAsync());
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
            (attachment.Altinn2AttachmentId == null ? "" :
            $"&altinn2AttachmentId={HttpUtility.UrlEncode(attachment.Altinn2AttachmentId?.ToString() ?? "")}");
    }

    [Fact]
    public async Task InitializeMigrateAttachment_InitializeAndUpload_NewUploadEndpoint()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _client.PostAsync(command, content);
        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_UploadBothAttachments_ThenInitializeCorrespondence()
    {
        MigrateInitializeAttachmentExt migrateAttachmentExt = new MigrateAttachmentBuilder().CreateAttachment().Build();
        byte[] file = Encoding.UTF8.GetBytes("Test av fil opplasting");
        MemoryStream memoryStream = new(file);
        StreamContent content = new(memoryStream);
        string command = GetAttachmentCommand(migrateAttachmentExt);
        var uploadResponse = await _client.PostAsync(command, content);
        Guid attachmentId = Guid.Parse(uploadResponse.Content.ReadAsStringAsync().Result.Trim('"'));

        MigrateInitializeAttachmentExt migrateAttachmentExt2 = new MigrateAttachmentBuilder().CreateAttachment().Build();
        byte[] file2 = Encoding.UTF8.GetBytes("Test av fil 2 opplasting");
        MemoryStream memoryStream2 = new(file2);
        StreamContent content2 = new(memoryStream2);
        string command2 = GetAttachmentCommand(migrateAttachmentExt2);
        var uploadResponse2 = await _client.PostAsync(command2, content);
        Guid attachmentId2 = Guid.Parse(uploadResponse2.Content.ReadAsStringAsync().Result.Trim('"'));

        InitializeCorrespondencesExt initializeCorrespondencesExt = new CorrespondenceBuilder().CreateCorrespondence().WithExistingAttachments([attachmentId, attachmentId2]).Build();
        initializeCorrespondencesExt.Correspondence.SendersReference = "test 2024 10 09 09 45";
        Guid userPartyGuid = new Guid("11112222333344445555666677778888");
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            Created = new DateTimeOffset(new DateTime(2024, 1, 5)),
            CorrespondenceData = initializeCorrespondencesExt,
            Altinn2CorrespondenceId = 12345,
            EventHistory = [ new MigrateCorrespondenceStatusEventExt()
            {
                Status = CorrespondenceStatusExt.Initialized, StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)), EventUserPartyUuid=userPartyGuid
            }
            ]
        };

        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);

        Assert.True(uploadResponse.IsSuccessStatusCode, uploadResponse.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        Assert.True(uploadResponse2.IsSuccessStatusCode, uploadResponse2.ReasonPhrase + ":" + await uploadResponse.Content.ReadAsStringAsync());
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, initializeCorrespondenceResponse.ReasonPhrase + ":" + await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_IsMigrateTrue_SetsFlagToTrue()
    {
        // Arrange
        var basicCorrespondence = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .Build();
        Guid userPartyGuid = new Guid("11112222333344445555666677778888");
        MigrateCorrespondenceExt migrateCorrespondenceExt = new()
        {
            Created = new DateTimeOffset(new DateTime(2024, 1, 5)),
            CorrespondenceData = basicCorrespondence,
            Altinn2CorrespondenceId = 12345,
            EventHistory =
            [
                new MigrateCorrespondenceStatusEventExt()
                {
                    Status = CorrespondenceStatusExt.Initialized,
                    StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                    EventUserPartyUuid = userPartyGuid
                }
            ],
            IsMigrating = true
        };

        // Act
        var initializeCorrespondenceResponse = await _client.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        CorrespondenceMigrationStatusExt? result = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(_responseSerializerOptions);
        Assert.NotNull(result);
        var scope = _factory.Services.CreateScope();
        var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
        CorrespondenceEntity? correspondence = await correspondenceRepository.GetCorrespondenceById(result.CorrespondenceId, true, true, false, CancellationToken.None);

        // Assert
        Assert.NotNull(correspondence);
        Assert.True(correspondence.IsMigrating);
    }
}
