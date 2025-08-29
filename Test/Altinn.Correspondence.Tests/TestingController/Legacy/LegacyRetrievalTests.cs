using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Legacy.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Altinn.Correspondence.Core.Services;
using Moq;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Tests.Fixtures;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingController.Legacy
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class LegacyRetrievalTests(CustomWebApplicationFactory factory) : LegacyTestBase(factory)
    {
        [Fact]
        public async Task LegacyGetCorrespondenceOverview_WithValidRequest_ReturnsOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceOverview_InvalidPartyId_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            var failClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123abc"));

            // Act
            var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceOverview_CorrespondenceNotPublished_Returns404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_WithValidRequest_ReturnsOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            var failClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123abc"));

            // Act
            var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_WithCorrespondenceActions_IncludesStatuses()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert
            var content = await response.Content.ReadFromJsonAsync<List<LegacyGetCorrespondenceHistoryResponse>>(_serializerOptions);
            Assert.NotNull(content);
            Assert.Contains(content, status => status.User.PartyId == _digdirPartyId);
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Published.ToString()));
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Confirmed.ToString()));
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Archived.ToString()));
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_WithCorrespondenceActionsByDifferentParties_IncludesStatuses()
        {
            // Arrange
            // TODO: Calls from LegacyClient / Altinn 2 Portal will always refer to the PartyID of the person logged in, so all these tests should be rewritten to refelct that reality.
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
                        
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/markAsRead", null);
            var secondClient = CreateLegacyTestClient(_delegatedUserPartyid);
            await secondClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/markasread", null);
            await secondClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert
            var content = await response.Content.ReadFromJsonAsync<List<LegacyGetCorrespondenceHistoryResponse>>(_serializerOptions);
            Assert.NotNull(content);
            Assert.Contains(content, status => status.User.PartyId == _digdirPartyId);
            Assert.Contains(content, status => status.User.PartyId == _digdirPartyId && status.Status.Contains(CorrespondenceStatus.Published.ToString()));
            Assert.Contains(content, status => status.User.PartyId == _digdirPartyId && status.Status.Contains(CorrespondenceStatus.Read.ToString()));
            Assert.Contains(content, status => status.User.PartyId == _delegatedUserPartyid);
            Assert.Contains(content, status => status.User.PartyId == _delegatedUserPartyid && status.Status.Contains(CorrespondenceStatus.Read.ToString()));
            Assert.Contains(content, status => status.User.PartyId == _delegatedUserPartyid && status.Status.Contains(CorrespondenceStatus.Confirmed.ToString()));
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_Attachment_Contains_name()
        {

            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _serializerOptions);
            var payload = new CorrespondenceBuilder().CreateCorrespondence().WithExistingAttachments([attachmentId]).Build();

            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            var content = await response.Content.ReadFromJsonAsync<LegacyCorrespondenceOverviewExt>(_serializerOptions);
            Assert.NotNull(content);
            Assert.Equal("Test file", content.Attachments.First().Name);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsUnauthorized()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            using var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Party)null);
                services.AddSingleton(mockRegisterService.Object);
            });
            var failClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123"));

            // Act
            var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_MigratedCorrespondence_WithForwardingEvents()
        {
            // Arrange
            InitializeCorrespondencesExt basicCorrespondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
            MigrateInitializeCorrespondencesExt migrateCorrespondence = new()
            {
                Correspondence = basicCorrespondence.Correspondence,
                Recipients = basicCorrespondence.Recipients,
                ExistingAttachments = basicCorrespondence.ExistingAttachments,
                IdempotentKey = basicCorrespondence.IdempotentKey
            };
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            migrateCorrespondence.Correspondence.Content.MessageBody = "<html><header>test header</header><body>test body</body></html>";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            MigrateCorrespondenceExt migrateCorrespondenceExt = new()
            {
                CorrespondenceData = migrateCorrespondence,
                Altinn2CorrespondenceId = 12345,
                EventHistory =
                [
                    new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = MigrateCorrespondenceStatusExt.Initialized,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                        EventUserPartyUuid = Guid.NewGuid()
                    }, new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = MigrateCorrespondenceStatusExt.Published,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 5)),
                        EventUserPartyUuid = Guid.NewGuid()
                    },
                    new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = MigrateCorrespondenceStatusExt.Read,
                        StatusChanged = new DateTimeOffset(new DateTime(2024, 1, 6)),
                        EventUserPartyUuid = Guid.NewGuid()
                    },
                    new MigrateCorrespondenceStatusEventExt()
                    {
                        Status = MigrateCorrespondenceStatusExt.Archived,
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
                IsReminder = true
            }
            ];

            migrateCorrespondenceExt.ForwardingHistory = new List<MigrateCorrespondenceForwardingEventExt>
            {
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sendt to own email address
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,0 ,0)),
                   ForwardedByPartyUuid = _delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToEmail = "user1@awesometestusers.com",
                   ForwardingText = "Keep this as a backup in my email."
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Copy sendt to own digital mailbox
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11 ,5 ,0)),
                   ForwardedByPartyUuid = _delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   MailboxSupplier = "urn:altinn:organization:identifier-no:123456789"
                },
                new MigrateCorrespondenceForwardingEventExt
                {
                   // Example of Instance Delegation by User 1 to User2
                   ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15 ,0)),
                   ForwardedByPartyUuid = _delegatedUserPartyUuid,
                   ForwardedByUserId = 123,
                   ForwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E"),
                   ForwardedToUserId = 456,
                   ForwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17"),
                   ForwardingText = "User2, - look into this for me please. - User1.",
                   ForwardedToEmail = "user2@awesometestusers.com"
                }
            };
            var migrationClient = _factory.CreateClientWithAddedClaims(("scope", "altinn:correspondence.migrate"));

            var migrateCorrespondenceResponse = await migrationClient.PostAsJsonAsync("correspondence/api/v1/migration/correspondence", migrateCorrespondenceExt);
            Assert.True(migrateCorrespondenceResponse.IsSuccessStatusCode, await migrateCorrespondenceResponse.Content.ReadAsStringAsync());
            var responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
            responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            CorrespondenceMigrationStatusExt? migrateResult = await migrateCorrespondenceResponse.Content.ReadFromJsonAsync<CorrespondenceMigrationStatusExt>(responseSerializerOptions);
            string correspondenceId = migrateResult?.CorrespondenceId.ToString();

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/history");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert
            var content = await response.Content.ReadFromJsonAsync<List<LegacyCorrespondenceHistoryExt>>(_serializerOptions);
            Assert.NotNull(content);            
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Published.ToString()));
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Read.ToString()));
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Archived.ToString()));

            // Assert Forwarding Events appear in correct form
            Assert.Contains(content, forwarding => forwarding.Status.Contains("ElementForwarded") && forwarding.ForwardingEvent.ForwardedByUserId.Equals(123)
                && forwarding.User != null && forwarding.User.PartyId == _delegatedUserPartyid && forwarding.User.Name.Equals(_delegatedUserName)
                && !String.IsNullOrEmpty(forwarding.ForwardingEvent.ForwardedToEmail) && forwarding.ForwardingEvent.ForwardedToEmail.Equals("user1@awesometestusers.com")
                && !String.IsNullOrEmpty(forwarding.ForwardingEvent.ForwardingText) && forwarding.ForwardingEvent.ForwardingText.Equals("Keep this as a backup in my email."));
            Assert.Contains(content, forwarding => forwarding.Status.Contains("ElementForwarded") && forwarding.ForwardingEvent.ForwardedByUserId.Equals(123)
                && forwarding.User != null && forwarding.User.PartyId == _delegatedUserPartyid && forwarding.User.Name.Equals(_delegatedUserName)
                && !String.IsNullOrEmpty(forwarding.ForwardingEvent.MailboxSupplier) && forwarding.ForwardingEvent.MailboxSupplier.Equals("123456789"));
            Assert.Contains(content, forwarding => forwarding.Status.Contains("ElementForwarded") && forwarding.ForwardingEvent.ForwardedByUserId.Equals(123)
                && forwarding.User != null && forwarding.User.PartyId == _delegatedUserPartyid && forwarding.User.Name.Equals(_delegatedUserName)
                && forwarding.ForwardingEvent.ForwardedToUserId == 456
                && !String.IsNullOrEmpty(forwarding.ForwardingEvent.ForwardedToEmail) && forwarding.ForwardingEvent.ForwardedToEmail.Equals("user2@awesometestusers.com")
                && !String.IsNullOrEmpty(forwarding.ForwardingEvent.ForwardingText) && forwarding.ForwardingEvent.ForwardingText.Equals("User2, - look into this for me please. - User1."));
        }
    }
}
