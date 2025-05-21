using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Migration.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text;
using System.Web;

namespace Altinn.Correspondence.Tests.TestingController.Migration;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class MigrationControllerTests : MigrationTestBase
{
    public MigrationControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {  
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();

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

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_WithForwarded()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
            .CreateMigrateCorrespondence()
            .WithIsMigrating(false)
            .WithStatusEvent(CorrespondenceStatusExt.Read, new DateTime(2024, 1, 6))
            .WithStatusEvent(CorrespondenceStatusExt.Archived, new DateTime(2024, 1, 7))
            .Build();

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

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
        string result = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
        Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, result);
    }

    [Fact]
    public async Task InitializeMigrateCorrespondence_NotReadNoNotifications()
    {
        MigrateCorrespondenceExt migrateCorrespondenceExt = new MigrateCorrespondenceBuilder()
             .CreateMigrateCorrespondence()
             .WithIsMigrating(false)
             .Build();

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
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

        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);

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
        var initializeCorrespondenceResponse = await _migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
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
}
