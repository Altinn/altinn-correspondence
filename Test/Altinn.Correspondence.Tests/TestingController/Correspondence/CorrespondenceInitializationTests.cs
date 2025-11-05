using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Core.Repositories;
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
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Tests.Extensions;
using Altinn.Correspondence.Integrations.Dialogporten.Models;

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
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task InitializeCorrespondence_WithNullOrEmptyLanguageCode_DefaultsToNb_ReturnsOK(string? languageCode)
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
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
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
            // Temporarily disabled until changed by customer #1331
            return;
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
        public async Task InitializeCorrespondence_WithTitleTooLong_ReturnsBadRequest()
        {
            // Arrange - Create a title that exceeds 255 characters
            var longTitle = new string('A', 256); // 256 characters to exceed the limit
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle(longTitle)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithTitleAt255Characters_Succeeds()
        {
            // Arrange - Create a title exactly at the 255 character limit
            var maxLengthTitle = new string('A', 255); // Exactly 255 characters
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle(maxLengthTitle)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
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

        [Theory]
        [InlineData("Data/Markdown1.txt")]
        [InlineData("Data/Markdown2.txt")]
        [InlineData("Data/Markdown3.txt")]
        public async Task InitializeCorrespondence_With_Different_Markdown_In_Body(string filePath)
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody(File.ReadAllText(filePath))
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
        public async Task InitializeCorrespondence_WithMoreThan100ExistingAttachments_ReturnsBadRequest()
        {
            // Arrange
            var existing = new List<Guid>();
            for (int i = 0; i < 101; i++)
            {
                var id = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
                existing.Add(id);
            }

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments(existing)
                .Build();

            // Act
            var response = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains(CorrespondenceErrors.AttachmentCountExceeded.Message, body);
        }

        [Fact]
        public async Task InitializeCorrespondence_With100ExistingAttachments_ReturnsOk()
        {
            // Arrange
            var existing = new List<Guid>();
            for (int i = 0; i < 100; i++)
            {
                var id = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
                existing.Add(id);
            }

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments(existing)
                .Build();

            // Act
            var response = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMoreThan100ExistingAndNewAttachmentsCombined_ReturnsBadRequest()
        {
            // Arrange
            var existing = new List<Guid>();
            for (int i = 0; i < 50; i++)
            {
                var id = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
                existing.Add(id);
            }

            var newAttachments = new List<InitializeCorrespondenceAttachmentExt>();
            for (int i = 0; i < 51; i++)
            {
                var attachment = AttachmentHelper.GetAttachmentMetaData($"file-combined-{i}.txt");
                attachment.DataLocationType = InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment;
                attachment.ExpirationTime = DateTimeOffset.UtcNow.AddDays(1);
                newAttachments.Add(attachment);
            }

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments(existing)
                .WithAttachments(newAttachments)
                .Build();

            // Act
            var response = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains(CorrespondenceErrors.AttachmentCountExceeded.Message, body);
        }

        [Fact]
        public async Task InitializeCorrespondence_MultipleAttachments_Succeeds()
        {
            // Arrange
            var attachments = new List<InitializeCorrespondenceAttachmentExt>();
            var attachmentFiles = new[] { "document1.pdf", "image2.jpg", "data3.xlsx" };

            foreach (var fileName in attachmentFiles)
            {
                var attachment = AttachmentHelper.GetAttachmentMetaData(fileName);
                attachment.DataLocationType = InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment;
                attachment.ExpirationTime = DateTimeOffset.UtcNow.AddYears(1);
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
                    memoryStream.Write(Encoding.UTF8.GetBytes($"test content for {attachment.FileName}"));
                    memoryStream.Position = 0;

                    var streamContent = new StreamContent(memoryStream);
                    formData.Add(streamContent, "attachments", attachment.FileName);
                }

                // Act
                var response = await _senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var responseContent = await response.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
                Assert.NotNull(responseContent);
                Assert.NotEmpty(responseContent.Correspondences);
                Assert.Equal(3, responseContent.AttachmentIds.Count());
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
        public async Task InitializeCorrespondence_MultipleAttachmentsButOneMalware_SucceedsInitializeButFailedOnPoll()
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
            using var memoryStream1 = new MemoryStream();
            using var memoryStream2 = new MemoryStream();
            using var memoryStream3 = new MemoryStream();

            memoryStream1.Write("safe content"u8);
            memoryStream2.Write("malware content"u8);
            memoryStream3.Write("safe content 2"u8);

            var filename1 = $"safe1-{Guid.NewGuid()}.txt";
            var filename2 = $"malware-{Guid.NewGuid()}.txt";
            var filename3 = $"safe2-{Guid.NewGuid()}.txt";

            var file1 = new FormFile(memoryStream1, 0, memoryStream1.Length, "file", filename1);
            var file2 = new FormFile(memoryStream2, 0, memoryStream2.Length, "file", filename2);
            var file3 = new FormFile(memoryStream3, 0, memoryStream3.Length, "file", filename3);

            var attachmentData1 = AttachmentHelper.GetAttachmentMetaData(filename1);
            var attachmentData2 = AttachmentHelper.GetAttachmentMetaData(filename2);
            var attachmentData3 = AttachmentHelper.GetAttachmentMetaData(filename3);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithAttachments([attachmentData1, attachmentData2, attachmentData3])
                .Build();

            var formData = CorrespondenceHelper.CorrespondenceToFormData(payload.Correspondence);
            formData.Add(new StringContent($"{UrnConstants.OrganizationNumberAttribute}:986252932"), "recipients[0]");

            using var fileStream1 = file1.OpenReadStream();
            using var fileStream2 = file2.OpenReadStream();
            using var fileStream3 = file3.OpenReadStream();
            formData.Add(new StreamContent(fileStream1), "attachments", file1.FileName);
            formData.Add(new StreamContent(fileStream2), "attachments", file2.FileName);
            formData.Add(new StreamContent(fileStream3), "attachments", file3.FileName);

            // Act
            var uploadCorrespondenceResponse = await senderClient.PostAsync("correspondence/api/v1/correspondence/upload", formData);
            Assert.NotNull(uploadCorrespondenceResponse);
            Assert.True(uploadCorrespondenceResponse.StatusCode == HttpStatusCode.OK, await uploadCorrespondenceResponse.Content.ReadAsStringAsync());
            var uploadResponseContent = await uploadCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            using var scope = testFactory.Services.CreateScope();
            using var applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Wait for attachments to be created in database
            int retryAttempts = 30;
            while (!applicationDbContext.Attachments.Any(attachment => attachment.FileName == filename1) ||
                   !applicationDbContext.Attachments.Any(attachment => attachment.FileName == filename2) ||
                   !applicationDbContext.Attachments.Any(attachment => attachment.FileName == filename3))
            {
                if (retryAttempts == 0)
                {
                    break;
                }
                retryAttempts--;
                await Task.Delay(100);
            }

            var attachment1 = applicationDbContext.Attachments.FirstOrDefault(attachment => attachment.FileName == filename1);
            var attachment2 = applicationDbContext.Attachments.FirstOrDefault(attachment => attachment.FileName == filename2);
            var attachment3 = applicationDbContext.Attachments.FirstOrDefault(attachment => attachment.FileName == filename3);

            Assert.NotNull(attachment1); // Safe attachment 1 not found in database
            Assert.NotNull(attachment2); // Malware attachment not found in database
            Assert.NotNull(attachment3); // Safe attachment 2 not found in database

            // Simulate malware scan results - one safe, one malware, one safe
            var jsonBody1 = MalwareScanResultControllerTests.GetMalwareScanResultJson("Data/MalwareScanResult_NoThreatFound.json", attachment1.Id.ToString());
            var jsonBody2 = MalwareScanResultControllerTests.GetMalwareScanResultJson("Data/MalwareScanResult_Malicious.json", attachment2.Id.ToString());
            var jsonBody3 = MalwareScanResultControllerTests.GetMalwareScanResultJson("Data/MalwareScanResult_NoThreatFound.json", attachment3.Id.ToString());

            var malwareScanHandler = scope.ServiceProvider.GetRequiredService<MalwareScanResultHandler>();
            var result1 = await webhookClient.PostAsync("correspondence/api/v1/webhooks/malwarescanresults", new StringContent(jsonBody1, Encoding.UTF8, "application/json"));
            await malwareScanHandler.CheckCorrespondenceStatusesAfterDeleteAndPublish(attachment1.Id, Guid.NewGuid(), CancellationToken.None);
            var result2 = await webhookClient.PostAsync("correspondence/api/v1/webhooks/malwarescanresults", new StringContent(jsonBody2, Encoding.UTF8, "application/json"));
            await malwareScanHandler.CheckCorrespondenceStatusesAfterDeleteAndPublish(attachment2.Id, Guid.NewGuid(), CancellationToken.None);
            var result3 = await webhookClient.PostAsync("correspondence/api/v1/webhooks/malwarescanresults", new StringContent(jsonBody3, Encoding.UTF8, "application/json"));
            await malwareScanHandler.CheckCorrespondenceStatusesAfterDeleteAndPublish(attachment3.Id, Guid.NewGuid(), CancellationToken.None);

            // Assert

            // Verify that the correspondence was scheduled to be failed because of one malware attachment
            hangfireBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job => job.Method.Name == "FailAssociatedCorrespondences"),
                It.IsAny<IState>()), Times.Once);
            var correspondenceId = uploadResponseContent.Correspondences.First().CorrespondenceId;
            await malwareScanHandler.FailAssociatedCorrespondences(attachment2.Id, Guid.NewGuid(), CancellationToken.None);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, uploadResponseContent.Correspondences.First().CorrespondenceId, CorrespondenceStatusExt.Failed);
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

        [Fact]
        public async Task InitializeCorrespondence_WithMultipleRecipients_ReplyOptionsPresentForAll()
        {
            // Arrange
            var recipients = new List<string>
            {
                $"{UrnConstants.OrganizationNumberAttribute}:986252932",
                $"{UrnConstants.OrganizationNumberAttribute}:991234649"
            };

            var replyOptions = new List<CorrespondenceReplyOptionExt>
            {
                new CorrespondenceReplyOptionExt
                {
                    LinkURL = "https://www.altinn.no",
                    LinkText = "Altinn"
                }
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(recipients)
                .WithReplyOptions(replyOptions)
                .Build();

            // Act
            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
            var initContent = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(initContent);
            Assert.True(initContent.Correspondences.Count >= recipients.Count);

            foreach (var created in initContent.Correspondences)
            {
                var overviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{created.CorrespondenceId}");
                overviewResponse.EnsureSuccessStatusCode();
                var overview = await overviewResponse.Content.ReadFromJsonAsync<GetCorrespondenceOverviewResponse>(_responseSerializerOptions);
                Assert.NotNull(overview);
                Assert.NotEmpty(overview.ReplyOptions);
                Assert.Contains(overview.ReplyOptions, ro => ro.LinkURL == replyOptions.First().LinkURL && ro.LinkText == replyOptions.First().LinkText);
            }
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMultipleRecipients_PropertyListPresentForAll()
        {
            // Arrange
            var recipients = new List<string>
            {
                $"{UrnConstants.OrganizationNumberAttribute}:986252932",
                $"{UrnConstants.OrganizationNumberAttribute}:991234649"
            };

            var propertyList = new Dictionary<string, string>
            {
                {"CaseId", "ABC-123"},
                {"Department", "IT"}
            };

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(recipients)
                .Build();

            payload.Correspondence.PropertyList = propertyList;

            // Act
            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
            var initContent = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(initContent);
            Assert.True(initContent.Correspondences.Count >= recipients.Count);

            foreach (var created in initContent.Correspondences)
            {
                var overviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{created.CorrespondenceId}");
                overviewResponse.EnsureSuccessStatusCode();
                var overview = await overviewResponse.Content.ReadFromJsonAsync<GetCorrespondenceOverviewResponse>(_responseSerializerOptions);
                Assert.NotNull(overview);
                Assert.NotNull(overview.PropertyList);
                Assert.True(propertyList.All(kv => overview.PropertyList.ContainsKey(kv.Key) && overview.PropertyList[kv.Key] == kv.Value));
            }
        }

        [Fact]
        public async Task InitializeCorrespondence_WithSummaryTooLong_ReturnsBadRequest()
        {
            // Arrange - Create a summary that exceeds 255 characters
            var longSummary = new string('A', 256);
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageSummary(longSummary)
            .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

        }

        [Fact]
        public async Task InitializeCorrespondence_WithSummaryAt255Characters_Succeeds()
        {
            // Arrange - Create a summary exactly at the 255 character limit
            var maxLengthSummary = new string('A', 255);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageSummary(maxLengthSummary)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithSenderAt255Characters_Succeeds()
        {
            // Arrange - Create a sender exactly at the 255 character limit
            var maxLengthSender = new string('A', 255);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageSender(maxLengthSender)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithSenderTooLong_ReturnsBadRequest()
        {
            // Arrange - Create a sender that exceeds the 255 character limit
            var longSender = new string('A', 256);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageSender(longSender)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Theory]
        [InlineData("<h1>test</h1>")]
        [InlineData("# test")]
        public async Task InitializeCorrespondence_With_HTML_Or_Markdown_In_Sender_fails(string messageSender)
        {
            var payload = new CorrespondenceBuilder()
            .CreateCorrespondence()
            .WithMessageSender(messageSender)
            .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMessageBodyTooLong_ReturnsBadRequest()
        {
            // Arrange - Create a message body that exceeds 30000 characters
            var longMessageBody = new string('A', 30001);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody(longMessageBody)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMessageBodyAt30000Characters_Succeeds()
        {
            // Arrange - Create a message body exactly at the 30000 character limit
            var maxLengthMessageBody = new string('A', 30000);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody(maxLengthMessageBody)
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMessageBodyEmpty_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageBody("")
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithNonExistentRecipient_ReturnsNotFound()
        {
            // Arrange
            var nonExistentRecipient = "0192:999999999"; // Organization number that doesn't exist
            var validRecipient = "0192:986252932"; // Valid organization number

            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();

                // Mock to return null for the non-existent recipient
                mockRegisterService
                    .Setup(service => service.LookUpPartyById(nonExistentRecipient, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Party?)null);

                // Mock to return a valid party for existing recipients
                mockRegisterService
                    .Setup(service => service.LookUpPartyById(validRecipient, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party { PartyUuid = Guid.NewGuid(), OrgNumber = "986252932" });

                // Mock for sender lookup (needed for authorization)
                mockRegisterService
                    .Setup(service => service.LookUpPartyById(It.Is<string>(s => s.Contains("991825827")), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party { PartyUuid = Guid.NewGuid(), OrgNumber = "991825827" });

                services.AddSingleton(mockRegisterService.Object);
            });

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([nonExistentRecipient])
                .Build();

            // Act
            var senderClient = testFactory.CreateSenderClient();
            var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, initializeCorrespondenceResponse.StatusCode);
            var errorContent = await initializeCorrespondenceResponse.Content.ReadAsStringAsync();
            Assert.Contains("Could not find partyId for the following recipients", errorContent);
            Assert.Contains(nonExistentRecipient.WithoutPrefix(), errorContent);
        }
        [Fact]
        public async Task InitializeCorrespondence_WithDialogportenDialogId_CreatesTransmission_Succeeds()
        {
            // Arrange
            var correspondence1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("First Title")
                .Build();


            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, correspondence1);
            var correspondenceContent = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using var scope = _factory.Services.CreateScope();
            var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();

            var correspondence = await correspondenceRepository.GetCorrespondenceById(
                initializedCorrespondence.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var externalReference = correspondence?.ExternalReferences;
            var dialogId = externalReference.First().ReferenceValue;
            Assert.NotNull(dialogId);


            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExternalReferencesDialogId(dialogId)
                .Build();

            var initializedTransmission = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload2);
            var transmissionContent = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, initializedTransmission.CorrespondenceId, CorrespondenceStatusExt.Published);
            var transmission = await correspondenceRepository.GetCorrespondenceById(
                transmissionContent.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);
            var transmissionExternalReference = transmission?.ExternalReferences;
            Assert.Equal(2, transmissionExternalReference.Count);
            Assert.Contains("DialogportenTransmissionId", transmissionExternalReference.Select(r => r.ReferenceType.ToString()));
        }

        [Fact]
        public async Task InitializeCorrespondence_WithDialogportenTransmissionId_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExternalReferencesTransmissionId()
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Theory]
        [InlineData(false, false, HttpStatusCode.BadRequest)]
        [InlineData(true, false, HttpStatusCode.BadRequest)]
        [InlineData(false, true, HttpStatusCode.OK)]
        [InlineData(true, true, HttpStatusCode.OK)]
        public async Task InitializeCorrespondence_ValidatesRolesForOrgRecipient(bool subUnit, bool hasRequiredRoles, HttpStatusCode expectedStatus)
        {
            // Arrange
            var orgNo = "100000001";
            var mainUnitOrgNo = "100000002";
            using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();

                mockRegisterService.SetupPartyByIdLookup("991825827", Guid.NewGuid());

                var recipientPartyUuid = Guid.NewGuid();
                var mainUnitPartyUuid = Guid.NewGuid();
                mockRegisterService.SetupPartyByIdLookup(orgNo, recipientPartyUuid);

                if (subUnit) mockRegisterService.SetupMainUnitsLookup(orgNo, mainUnitOrgNo, mainUnitPartyUuid);
                else mockRegisterService.SetupEmptyMainUnitsLookup(orgNo);

                if (hasRequiredRoles)
                {
                    mockRegisterService.SetupPartyRoleLookup(recipientPartyUuid.ToString(), "daglig-leder");
                    mockRegisterService.SetupPartyRoleLookup(mainUnitPartyUuid.ToString(), "daglig-leder");
                }
                else
                {
                    mockRegisterService.SetupPartyRoleLookup(recipientPartyUuid.ToString(), "ANNET");
                    mockRegisterService.SetupPartyRoleLookup(mainUnitPartyUuid.ToString(), "ANNET");
                }

                services.AddSingleton(mockRegisterService.Object);
            });

            var recipientUrn = orgNo.WithUrnPrefix();
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients([recipientUrn])
                .WithIsConfidential(true)
                .Build();

            // Act
            var senderClient = testFactory.CreateSenderClient();
            var response = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedStatus == HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Assert.Contains("lack required roles", errorContent);
            }
        }

        [Fact]
        public async Task InitializeCorrespondence_WithPropertyListTooLong_ReturnsBadRequest()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithPropertyList(new Dictionary<string, string> { { "key", "value" },
                { "key2", "value2" },
                { "key3", "value3" },
                { "key4", "value4" },
                { "key5", "value5" },
                { "key6", "value6" },
                { "key7", "value7" },
                { "key8", "value8" },
                { "key9", "value9" },
                { "key10", "value10" },
                { "key11", "value11" } })
                .Build();

            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceContent = await initializeResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_TransmissionScheduledAtRequestedPublishTime_NotPublishedBefore()
        {
            // Arrange
            var futurePublishTime = DateTimeOffset.UtcNow.AddHours(2);

            var correspondence1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("First Correspondence")
                .Build();

            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, correspondence1);
            var correspondenceContent = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using var scope = _factory.Services.CreateScope();
            var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();

            var correspondence = await correspondenceRepository.GetCorrespondenceById(
                initializedCorrespondence.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var dialogId = correspondence?.ExternalReferences
                .FirstOrDefault(er => er.ReferenceType == Core.Models.Enums.ReferenceType.DialogportenDialogId)?.ReferenceValue;
            Assert.NotNull(dialogId);

            var transmissionPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("Transmission Correspondence")
                .WithExternalReferencesDialogId(dialogId)
                .WithRequestedPublishTime(futurePublishTime)
                .Build();

            // Act
            var transmissionResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", transmissionPayload);
            Assert.Equal(HttpStatusCode.OK, transmissionResponse.StatusCode);

            var transmissionContent = await transmissionResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(transmissionContent);

            var transmissionId = transmissionContent.Correspondences.First().CorrespondenceId;

            // Assert 
            var statusResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{transmissionId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusContent = await statusResponse.Content.ReadFromJsonAsync<GetCorrespondenceOverviewResponse>(_responseSerializerOptions);

            Assert.NotNull(statusContent);
            Assert.NotEqual("Published", statusContent.Status.ToString());

            var transmissionEntity = await correspondenceRepository.GetCorrespondenceById(
                transmissionId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var transmissionReference = transmissionEntity?.ExternalReferences
                .FirstOrDefault(er => er.ReferenceType == Core.Models.Enums.ReferenceType.DialogportenDialogId);

            // The transmission reference should contain a dialog id, but not a transmission id as this is set upon publishing
            Assert.NotNull(transmissionReference);
            Assert.Equal(1, transmissionEntity?.ExternalReferences.Count);
        }
        [Fact]
        public async Task InitializeCorrespondenceTransmission_WithTwoRecipients_ReturnsBadRequest()
        {
            var correspondence1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("First Title")
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, correspondence1);
            var correspondenceContent = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using var scope = _factory.Services.CreateScope();
            var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();

            var correspondence = await correspondenceRepository.GetCorrespondenceById(
                initializedCorrespondence.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var externalReference = correspondence?.ExternalReferences;
            Assert.NotNull(externalReference);
            var dialogId = externalReference.First().ReferenceValue;
            Assert.NotNull(dialogId);

            var transmissionPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["26818099001", "07827199405"])
                .WithExternalReferencesDialogId(dialogId)
                .Build();

            // Act
            var transmissionResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", transmissionPayload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, transmissionResponse.StatusCode);
        }


        [Fact]
        public async Task InitializeCorrespondenceTransmission_WithRecipientMismatch_ReturnsBadRequest()
        {
            // Create a custom factory with mock validation that returns false for mismatched recipients
            var mockDialogportenService = new Mock<IDialogportenService>();
            mockDialogportenService.Setup(x => x.ValidateDialogRecipientMatch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((bool?)false); // Different recipient should fail validation

            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IDialogportenService));
                if (serviceDescriptor != null)
                {
                    services.Remove(serviceDescriptor);
                }
                services.AddScoped(_ => mockDialogportenService.Object);
            });
            var client = customFactory.CreateSenderClient();


            var correspondence1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("First Title")
                .WithRecipients(["26818099001"])
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(client, _responseSerializerOptions, correspondence1);
            var correspondenceContent = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(client, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using var scope = customFactory.Services.CreateScope();
            var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();

            var correspondence = await correspondenceRepository.GetCorrespondenceById(
                initializedCorrespondence.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var externalReference = correspondence?.ExternalReferences;
            Assert.NotNull(externalReference);
            var dialogId = externalReference.First().ReferenceValue;
            Assert.NotNull(dialogId);

            var transmissionPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["07827199405"]) // Different recipient than original correspondence
                .WithExternalReferencesDialogId(dialogId)
                .Build();

            // Act
            var transmissionResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", transmissionPayload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, transmissionResponse.StatusCode);

            // Clean up
            customFactory.Dispose();
        }

        [Fact]
        public async Task InitializeCorrespondence_WithIdempotentKeyAndMultipleRecipients_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("First Title")
                .WithIdempotentKey(Guid.NewGuid())
                .WithRecipients(["26818099001", "07827199405"])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithDialogIdNotGuid_ReturnsBadRequest()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExternalReferencesDialogId("not-a-guid")
                .Build();

            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceContent = await initializeResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, initializeResponse.StatusCode);
            Assert.Contains(CorrespondenceErrors.InvalidCorrespondenceDialogId.Message, correspondenceContent);
        }

        [Fact]
        public async Task InitializeCorrespondence_CreateTransmission_WithDialogIdNotFoundInDialogporten_ReturnsBadRequest()
        {

            var mockDialogPortenService = new Mock<IDialogportenService>();
            mockDialogPortenService.Setup(x => x.DialogValidForTransmission(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((bool?)null); // Dialog not found
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
                {
                    var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IDialogportenService));
                    if (serviceDescriptor != null)
                    {
                        services.Remove(serviceDescriptor);
                    }
                    services.AddScoped(_ => mockDialogPortenService.Object);
                });

            var client = customFactory.CreateSenderClient();

            var transmissionPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExternalReferencesDialogId("00000000-0000-0000-0000-000000000000") // Valid GUID but not found
                .Build();

            // Act
            var transmissionResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", transmissionPayload);
            var transmissionContent = await transmissionResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, transmissionResponse.StatusCode);
            Assert.Contains(CorrespondenceErrors.DialogNotFoundWithDialogId.Message, transmissionContent);

            // Clean up
            customFactory.Dispose();
        }

        [Fact]
        public async Task InitializeCorrespondence_CreateTransmission_WithDifferentResource_SameServiceOwner_Succeeds()
        {
            // Arrange
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockDialogporten = new Mock<IDialogportenService>();
                mockDialogporten
                    .Setup(x => x.DialogValidForTransmission(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                mockDialogporten
                    .Setup(x => x.ValidateDialogRecipientMatch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IDialogportenService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockDialogporten.Object);
            });

            var client = customFactory.CreateSenderClient();

            
            var initialCorrespondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("Initial Correspondence")
                .WithResourceId("resource-A")
                .Build();

            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(client, _responseSerializerOptions, initialCorrespondence);
            var published = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(client, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using var scope = customFactory.Services.CreateScope();
            var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
            var correspondence = await correspondenceRepository.GetCorrespondenceById(
                initializedCorrespondence.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var dialogId = correspondence?.ExternalReferences
                .FirstOrDefault(er => er.ReferenceType == Core.Models.Enums.ReferenceType.DialogportenDialogId)?.ReferenceValue;
            Assert.NotNull(dialogId);

            // Act
            var transmissionPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("Transmission - Different Resource Same Owner")
                .WithExternalReferencesDialogId(dialogId!)
                .WithResourceId("resource-B")
                .Build();

            var transmissionResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", transmissionPayload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, transmissionResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_CreateTransmission_WithDifferentServiceOwner_ReturnsBadRequest()
        {
            // Arrange
            using var customFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockDialogporten = new Mock<IDialogportenService>();
                mockDialogporten
                    .Setup(x => x.DialogValidForTransmission(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);
                mockDialogporten
                    .Setup(x => x.ValidateDialogRecipientMatch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IDialogportenService));
                if (existing != null) services.Remove(existing);
                services.AddScoped(_ => mockDialogporten.Object);
            });

            var client = customFactory.CreateSenderClient();

            
            var initialCorrespondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("Initial correspondence")
                .WithResourceId("resource-1")
                .Build();

            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(client, _responseSerializerOptions, initialCorrespondence);
            var _ = await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(client, _responseSerializerOptions, initializedCorrespondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            using var scope = customFactory.Services.CreateScope();
            var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
            var correspondence = await correspondenceRepository.GetCorrespondenceById(
                initializedCorrespondence.CorrespondenceId,
                includeStatus: false,
                includeContent: false,
                includeForwardingEvents: false,
                cancellationToken: CancellationToken.None);

            var dialogId = correspondence?.ExternalReferences
                .FirstOrDefault(er => er.ReferenceType == Core.Models.Enums.ReferenceType.DialogportenDialogId)?.ReferenceValue;
            Assert.NotNull(dialogId);

            // Act
            var transmissionPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("Transmission")
                .WithExternalReferencesDialogId(dialogId!)
                .WithResourceId("Resource-2 with different owner")
                .Build();

            var transmissionResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", transmissionPayload);
            var content = await transmissionResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, transmissionResponse.StatusCode);
            Assert.Contains(CorrespondenceErrors.InvalidServiceOwner.Message, content);
        }
    }
}