using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Altinn.Correspondence.Persistence;
using System.Text;
using Altinn.Correspondence.Tests.TestingFeature;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceInitializationTests : CorrespondenceTestBase
    {
        public CorrespondenceInitializationTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task InitializeCorrespondence()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Theory]
        [InlineData("nN")]
        [InlineData("EN")]
        public async Task InitializeCorrespondence_WithCorrectLanguageCode_ReturnsOK(string languageCode)
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode(languageCode)
            .Build();

            // Act
            var response = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("nu")]
        [InlineData(null)]
        [InlineData("")]
        public async Task InitializeCorrespondence_WithInvalidLanguageCode_ReturnsBadRequest(string? languageCode)
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithLanguageCode(languageCode)
            .Build();

            // Act
            var response = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithExistingAttachmentsPublished_ReturnsOK()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InitializeCorrespondenceMultiple_WithExistingAttachmentsPublished_ReturnsOK()
        {
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRecipients([$"{UrnConstants.OrganizationNumberAttribute}:986252932", $"{UrnConstants.OrganizationNumberAttribute}:991234649"])
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InitializeCorrespondence_WithInvalidExistingAttachments_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([Guid.NewGuid()])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithExistingAttachmentsNotPublished_ReturnsBadRequest()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithoutContent_ReturnsBadRequest()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithCorrespondenceContent(null)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithEmptyMessageFields_ReturnsBadRequest()
        {
            // Arrange
            var payload1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("")
                .Build();

            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageSummary("")
                .Build();

            var payload3 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody("")
                .Build();

            // Act
            var response1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1);
            var response2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2);
            var response3 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload3);


            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
        }

        [Theory]
        [InlineData("<h1>test</h1>")]
        [InlineData("# test")]
        public async Task InitializeCorrespondence_With_HTML_Or_Markdown_In_Title_fails(string messageTitle)
        {
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageTitle(messageTitle)
            .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_With_HTML_In_Summary_Or_Body_fails()
        {
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageSummary("<h1>test</h1>")
            .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody("<h1>test</h1>")
                .Build();

            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeCorrespondence_With_RecipientToken_succeeds()
        {
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageSummary("test for {{recipientName}}")
            .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);

            payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageSummary("test for {{recipientName}}")
                .Build();

            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_No_Message_Body_fails()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody(null)
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_With_Different_Markdown_In_Body()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody(File.ReadAllText("Data/Markdown.txt"))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task InitializeCorrespondence_Recipient_Can_Handle_Org_And_Ssn()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["0192:123456789", "07827199405"])
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        }

        [Theory]
        [InlineData("invalid-recipient")]
        [InlineData("123456789")]
        [InlineData("1234567812390123")]
        [InlineData("1234:123456789")]
        public async Task InitializeCorrespondence_With_Invalid_Recipient_Returns_BadRequest(string recipient)
        {
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithRecipients([recipient])
            .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_As_Recipient_Is_Forbidden()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();

            // Act
            var initializeCorrespondenceResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_DueDate_PriorToday_Returns_BadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(-7))
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_DueDate_PriorRequestedPublishTime_Returns_BadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(7))
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(14))
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorToday_Returns_BadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAllowSystemDeleteAfter(DateTimeOffset.UtcNow.AddDays(-7))
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorRequestedPublishTime_Returns_BadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAllowSystemDeleteAfter(DateTimeOffset.UtcNow.AddDays(7))
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(14))
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(21)) // ensure DueDate is after RequestedPublishTime
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_AllowSystemDeleteAfter_PriorDueDate_Returns_BadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAllowSystemDeleteAfter(DateTimeOffset.UtcNow.AddDays(14))
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(7))
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(21))
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithConfirmationNeeded_Without_DueDate_Returns_BadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithConfirmationNeeded(true)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_With_RequestedPublishTime_Null()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(null)
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithoutUrnFormat_AddsUrnFormat()
        {
            // Arrange
            var orgRecipient = "0192:123456789";
            var personRecipient = "08900499559";
            var sender = "0192:991825827";
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithSender(sender)
                .WithRecipients([orgRecipient, personRecipient])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var initializeContent = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(initializeContent);
            var recipients = initializeContent.Correspondences.Select(c => c.Recipient).ToList();
            var overview = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{initializeContent.Correspondences.First().CorrespondenceId}");
            var overviewContent = await overview.Content.ReadFromJsonAsync<GetCorrespondenceOverviewResponse>(_responseSerializerOptions);
            Assert.NotNull(overviewContent);

            // Assert
            Assert.Equal(recipients.First(), $"{UrnConstants.OrganizationNumberAttribute}:{orgRecipient.WithoutPrefix()}");
            Assert.Equal(recipients.Last(), $"{UrnConstants.PersonIdAttribute}:{personRecipient}");
            Assert.Equal(overviewContent.Sender, $"{UrnConstants.OrganizationNumberAttribute}:{sender.WithoutPrefix()}");
        }

        [Fact]
        public async Task InitializeCorrespondence_OneOfRecipientsIsReserved_PartiallySucceeds()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([CustomWebApplicationFactory.ReservedSsn, "07827199405"])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            var responseObject = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(responseObject);
            Assert.True(responseObject.Correspondences.Exists(responseObject => responseObject.Status == CorrespondenceStatusExt.ReadyForPublish));
            Assert.True(responseObject.Correspondences.Exists(responseObject => responseObject.Status != CorrespondenceStatusExt.ReadyForPublish));
        }

        [Fact]
        public async Task InitializeCorrespondence_OnlyRecipientIsReserved_Fails()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([CustomWebApplicationFactory.ReservedSsn])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_RecipientIsReservedButIgnoreReservation_Succeeds()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([CustomWebApplicationFactory.ReservedSsn])
                .WithIgnoreReservation(true)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_KRRFails_CorrespondenceFails()
        {
            var contactReservationRegistry = new Mock<IContactReservationRegistryService>();
            contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.GetReservedRecipients(It.IsAny<List<string>>())).Throws<HttpRequestException>();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(contactReservationRegistry.Object);
            });
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([CustomWebApplicationFactory.ReservedSsn])
                .WithIgnoreReservation(false)
                .Build();

            // Act
            
            var initializeCorrespondenceResponse = await testFactory.CreateSenderClient().PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, initializeCorrespondenceResponse.StatusCode);
            var body = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(body.Status, (int)HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task InitializeCorrespondence_KRRFailsButIgnoreReservation_Succeeds()
        {
            var contactReservationRegistry = new Mock<IContactReservationRegistryService>();
            contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.GetReservedRecipients(It.IsAny<List<string>>())).Throws<HttpRequestException>();
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(contactReservationRegistry.Object);
            });
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([CustomWebApplicationFactory.ReservedSsn])
                .WithIgnoreReservation(true)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await testFactory.CreateSenderClient().PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task IntializeCorrespondence_WithValidReplyOptions_ReturnsOK()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithReplyOptions(new List<CorrespondenceReplyOptionExt>
                {
                    new CorrespondenceReplyOptionExt
                    {
                        LinkURL = "https://www.altinn.no",
                        LinkText = "Altinn"
                    }
                })
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeCorrespondence_WithInvalidReplyOptions_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithReplyOptions(new List<CorrespondenceReplyOptionExt>
                {
                    new CorrespondenceReplyOptionExt
                    {
                        LinkURL = "http://www.altinn.no",
                        LinkText = "Altinn"
                    }
                })
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload.Correspondence.ReplyOptions.First().LinkURL = "www.altinn.no";
            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload.Correspondence.ReplyOptions.First().LinkURL = "https://www.al tinn.no";
            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload.Correspondence.ReplyOptions.First().LinkURL = "C:\\Users\\User\\Desktop";
            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }
        [Fact]
        public async Task IntializeCorrespondence_WithMultipleRecipients_GivesUniqueAttachmentIds()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["26818099001", "07827199405"])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            var responseObject = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(responseObject);
            Assert.Equal(responseObject.AttachmentIds.Count(), responseObject.AttachmentIds.Distinct().Count());
        }

        [Fact]
        public async Task InitializeCorrespondence_WithABrokerService_FailsWithBadRequest()
        {
            // Arrange
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var resourceRegistryService = new Mock<IResourceRegistryService>();
                resourceRegistryService.Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("altinn-broker-test-resource");
                resourceRegistryService.Setup(x => x.GetResourceType(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("BrokerService");
                resourceRegistryService.Setup(x => x.GetServiceOwnerOrganizationNumber(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("991825827");
                services.AddScoped(_ => resourceRegistryService.Object);
            });
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();

            // Act
            var unitSenderClient = testFactory.CreateSenderClient();
            var initializeCorrespondenceResponse = await unitSenderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
            var responseObject = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<ProblemDetails>(_responseSerializerOptions);
            Assert.NotNull(responseObject);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithoutAttachments_SchedulesPublish()
        {
            // Arrange
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient.Setup(x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()))
                .Returns("123456");

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(hangfireBackgroundJobClient.Object);
            });
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();

            // Act
            var senderClient = testFactory.CreateSenderClient();
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            hangfireBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "SchedulePublishAtPublishTime"),
                It.IsAny<IState>()), Times.Once);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithPublishedAttachment_SchedulesPublishCorrespondenceJob()
        {
            // Arrange
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient.Setup(x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()))
                .Returns("123456");

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(hangfireBackgroundJobClient.Object);
            });

            using var scope = testFactory.Services.CreateScope();
            var attachmentHelper = scope.ServiceProvider.GetRequiredService<Application.Helpers.AttachmentHelper>();
            var senderClient = testFactory.CreateSenderClient();

            var attachmentId = await AttachmentHelper.GetInitializedAttachment(senderClient, _responseSerializerOptions);
            var attachment = new AttachmentBuilder().CreateAttachment().Build();
            await AttachmentHelper.UploadAttachment(attachmentId, senderClient);

            // Manually trigger malware scan simulation since background job client is mocked
            await attachmentHelper.SimulateMalwareScanResult(attachmentId);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            hangfireBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "SchedulePublishAtPublishTime"),
                It.IsAny<IState>()), Times.Once);
        }

        [Fact]
        public async Task InitializeCorrespondence_MalwareScanArrivesBeforeCorrespondence_SchedulesPublishCorrespondenceJob()
        {
            // Arrange
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient
            .Setup(x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()))
            .Returns<Job, IState>((job, state) =>
            {
                // Apply 5-second delay only for CreateDialogportenDialog to give us time to poll for attachments
                if (job.Method.Name == "CreateDialogportenDialog")
                {
                    Task.Delay(5000).Wait();
                    return "dialog-job-id-123";
                }

                // For other jobs, return immediately
                return "123456";
            });

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(hangfireBackgroundJobClient.Object);
            });
            using var webhookClient = testFactory.CreateClient();
            using var senderClient = testFactory.CreateSenderClient();
            using var memoryStream = new MemoryStream();
            memoryStream.Write("test"u8);
            var filename = $"{Guid.NewGuid().ToString()}.txt";
            var file = new FormFile(memoryStream, 0, memoryStream.Length, "file", filename);
            var attachmentData = AttachmentHelper.GetAttachmentMetaData(filename);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData])
                .Build();
            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");
            using var fileStream = file.OpenReadStream();
            formData.Add(new StreamContent(fileStream), "attachments", file.FileName);

            // Act
            var uploadCorrespondenceResponseTask = senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            using var scope = testFactory.Services.CreateScope();
            using var applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int retryAttempts = 30;
            while (!applicationDbContext.Attachments.Any(attachment => attachment.FileName == filename))
            {
                if (retryAttempts == 0)
                {
                    break;
                }
                retryAttempts--;
                await Task.Delay(100);
            }
            var attachment = applicationDbContext.Attachments.FirstOrDefault(attachment => attachment.FileName == filename);
            Assert.NotNull(attachment); // Attachment not found in database after 30 retries (3 seconds) of polling
            var jsonBody = MalwareScanResultControllerTests.GetMalwareScanResultJson("Data/MalwareScanResult_NoThreatFound.json", attachment.Id.ToString());
            var result = await webhookClient.PostAsync("correspondence/api/v1/webhooks/malwarescanresults", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            var uploadCorrespondenceResponse = await uploadCorrespondenceResponseTask;

            // Assert
            Assert.NotNull(uploadCorrespondenceResponse);
            Assert.True(uploadCorrespondenceResponse.StatusCode == HttpStatusCode.OK, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
            hangfireBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "SchedulePublishAtPublishTime"),
                It.IsAny<IState>()), Times.Once);
            hangfireBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "CreateDialogportenDialog"),
                It.IsAny<IState>()), Times.Once);

            // Teardown
            memoryStream.Dispose();
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMoreThan100Attachments_ReturnsBadRequest()
        {
            // Arrange
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient
            .Setup(x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()))
            .Returns<Job, IState>((job, state) =>
            {
                // Apply 5-second delay only for CreateDialogportenDialog to give us time to poll for attachments
                if (job.Method.Name == "CreateDialogportenDialog")
                {
                    Task.Delay(5000).Wait();
                    return "dialog-job-id-123";
                }

                // For other jobs, return immediately
                return "123456";
            });

            var attachments = new List<InitializeCorrespondenceAttachmentExt>();
            for (int i = 0; i < 101; i++)
            {
                var attachment = AttachmentHelper.GetAttachmentMetaData($"file{i}.txt");
                attachment.DataLocationType = API.Models.Enums.InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment;
                attachment.ExpirationTime = DateTimeOffset.UtcNow.AddDays(1);
                attachments.Add(attachment);
            }

            Assert.Equal(101, attachments.Count);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments(attachments)
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");

            var streamsToDispose = new List<MemoryStream>();

            try
            {
                foreach (var attachment in attachments)
                {
                    var memoryStream = new MemoryStream();
                    streamsToDispose.Add(memoryStream);
                    memoryStream.Write(Encoding.UTF8.GetBytes("test content"));
                    memoryStream.Position = 0;

                    var streamContent = new StreamContent(memoryStream);
                    formData.Add(streamContent, "attachments", attachment.FileName);
                }

                // Act
                var response = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
                foreach (var stream in streamsToDispose)
                {
                    stream.Dispose();
                }
            }
        }

        [Fact]
        public async Task InitializeCorrespondence_With100Attachments_ReturnsOk()
        {
            // Arrange
            var attachments = new List<InitializeCorrespondenceAttachmentExt>();
            for (int i = 0; i < 100; i++)
            {
                var attachment = AttachmentHelper.GetAttachmentMetaData($"file{i}.txt");
                attachment.DataLocationType = InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment;
                attachment.ExpirationTime = DateTimeOffset.UtcNow.AddDays(1);
                attachments.Add(attachment);
            }

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments(attachments)
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");

            var streamsToDispose = new List<MemoryStream>();

            try
            {
                foreach (var attachment in attachments)
                {
                    var memoryStream = new MemoryStream();
                    streamsToDispose.Add(memoryStream);
                    memoryStream.Write(Encoding.UTF8.GetBytes("test content"));
                    memoryStream.Position = 0;

                    var streamContent = new StreamContent(memoryStream);
                    formData.Add(streamContent, "attachments", attachment.FileName);
                }

                // Act
                var response = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                foreach (var stream in streamsToDispose)
                {
                    stream.Dispose();
                }
            }
        }

        [Fact]
        public async Task InitializeCorrespondence_WithDuplicateIdempotentKey_ReturnsConflict()
        {
            // Arrange
            var idempotentKey = Guid.NewGuid();
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithIdempotentKey(idempotentKey)
                .Build();

            // Act - First request
            var firstResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            // Act - Second request with same idempotent key
            var secondResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
            var errorContent = await secondResponse.Content.ReadAsStringAsync();
            Assert.Contains(CorrespondenceErrors.DuplicateInitCorrespondenceRequest.Message, errorContent);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithDifferentContentAndSameIdempotentKey_ShouldReturnConflict()
        {
            // Arrange
            var idempotentKey = Guid.NewGuid();
            var correspondence1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithIdempotentKey(idempotentKey)
                .WithMessageTitle("First Title")
                .Build();

            var correspondence2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithIdempotentKey(idempotentKey)
                .WithMessageTitle("Second Title")
                .Build();

            // Act
            var response1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence1);
            var response2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence2);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
            var errorContent = await response2.Content.ReadAsStringAsync();
            Assert.Contains(CorrespondenceErrors.DuplicateInitCorrespondenceRequest.Message, errorContent);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithInvalidIdempotentKey_ShouldReturnBadRequest()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithIdempotentKey(Guid.Empty)
                .Build();

            // Act
            var response = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Contains(CorrespondenceErrors.InvalidIdempotencyKey.Message, errorContent);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithConcurrentRequests_ShouldHandleCorrectly()
        {
            // Arrange
            var idempotentKey = Guid.NewGuid();
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithIdempotentKey(idempotentKey)
                .Build();

            // Act
            var tasks = new[]
            {
                _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence),
                _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence)
            };
            var responses = await Task.WhenAll(tasks);

            // Assert
            var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, successCount);
            Assert.Equal(1, conflictCount);

            // Verify error message for the conflict response
            var conflictResponse = responses.First(r => r.StatusCode == HttpStatusCode.Conflict);
            var errorContent = await conflictResponse.Content.ReadAsStringAsync();
            Assert.Contains(CorrespondenceErrors.DuplicateInitCorrespondenceRequest.Message, errorContent);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithUrnPrefixOnResourceId_Succeeds()
        {
            // Arrange
            var resourceId = $"{UrnConstants.Resource}:1";
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resourceId)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(response);
            Assert.NotEmpty(response.Correspondences);
        }
    }
}
