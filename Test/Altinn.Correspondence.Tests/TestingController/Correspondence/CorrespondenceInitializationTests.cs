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
        public async Task InitializeCorrespondence_WithInvalidLanguageCode_ReturnsBadRequest(string languageCode)
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
                .WithRecipients(["0192:123456789", "12345678901"])
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task InitializeCorrespondence_With_Invalid_Sender_Returns_BadRequest()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithSender("invalid-sender")
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
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
            var personRecipient = "01234567890";
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
                .WithRecipients([CustomWebApplicationFactory.ReservedSsn, "01234567890"])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            var responseObject = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(responseObject);
            Assert.True(responseObject.Correspondences.Exists(responseObject => responseObject.Status == API.Models.Enums.CorrespondenceStatusExt.Published));
            Assert.True(responseObject.Correspondences.Exists(responseObject => responseObject.Status != API.Models.Enums.CorrespondenceStatusExt.Published));
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
            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
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
            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
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
                .WithRecipients(["01234567891", "01234567890"])
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
            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var resourceRegistryService = new Mock<IResourceRegistryService>();
                resourceRegistryService.Setup(x => x.GetServiceOwnerOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("altinn-broker-test-resource");
                resourceRegistryService.Setup(x => x.GetResourceType(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("BrokerService");
                services.AddSingleton(resourceRegistryService.Object);
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
            var expectedJobId = "123456";
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            hangfireBackgroundJobClient.Setup(x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<IState>()))
                .Returns(expectedJobId);
                
            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
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
                It.Is<Job>(job => job.Method.Name == "SchedulePublish"),
                It.IsAny<IState>()), Times.Once);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithAttachments_DoesNotSchedulePublish()
        {
            // Arrange
            var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
            var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                services.AddSingleton(hangfireBackgroundJobClient.Object);
            });
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(testFactory.CreateSenderClient(), _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var senderClient = testFactory.CreateSenderClient();
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
            hangfireBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "PublishCorrespondence"),
                It.IsAny<IState>()), Times.Never);
        }
    }
}
