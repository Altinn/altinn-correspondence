﻿using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
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

        [Fact]
        public async Task InitializeCorrespondence_WithCorrectLanguageCode_ReturnsOK()
        {
            // Arrange
            var payload1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithLanguageCode("nN")
                .Build();
            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithLanguageCode("EN")
                .Build();

            // Act
            var response1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1);
            var response2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithInvalidLanguageCode_ReturnsBadRequest()
        {
            // Arrange
            var payload1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithLanguageCode("nu")
                .Build();
            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithLanguageCode(null)
                .Build();
            var payload3 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithLanguageCode("")
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

        [Fact]
        public async Task InitializeCorrespondence_With_HTML_Or_Markdown_In_Title_fails()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("<h1>test</h1>")
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithMessageTitle("# test")
                .Build();
            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
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
                .WithMessageBody(File.ReadAllText("Data/Markdown.text"))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task InitializeCorrespondence_Recipient_Can_Handle_Org_And_Ssn()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["1234:123456789", "12345678901"])
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

        [Fact]
        public async Task InitializeCorrespondence_With_Invalid_Recipient_Returns_BadRequest()
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["invalid-recipient"])
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload.Recipients = ["123456789"];
            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.Equal(HttpStatusCode.BadRequest, initializeCorrespondenceResponse.StatusCode);

            payload.Recipients = ["1234567812390123"];
            initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
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

    }
}
