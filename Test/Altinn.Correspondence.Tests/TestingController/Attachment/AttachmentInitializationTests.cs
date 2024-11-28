using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using System.Net.Http.Json;
using System.Net;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    public class AttachmentInitializationTests : AttachmentTestBase
    {
        public AttachmentInitializationTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }
        [Fact]
        public async Task InitializeAttachment()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);
            Assert.NotNull(attachmentId);
        }
        [Fact]
        public async Task InitializeAttachment_AsRecipient_ReturnsForbidden()
        {
            var attachment = new AttachmentBuilder().CreateAttachment().Build();
            var initializeAttachmentResponse = await _recipientClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.Forbidden, initializeAttachmentResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_As_Different_Sender_As_Token_ReturnsUnauthorized()
        {
            var attachment = new AttachmentBuilder().CreateAttachment().Build();
            var initializeAttachmentResponse = await _wrongSenderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.Unauthorized, initializeAttachmentResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_WithWrongSender_ReturnsBadRequest()
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithSender("invalid-sender")
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);

            var attachment2 = new AttachmentBuilder()
                .CreateAttachment()
                .WithSender("123456789")
                .Build();
            var initializeAttachmentResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment2);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse2.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_WithoutUrnFormat_AddsUrnFormat()
        {
            // Arrange
            var sender = "0192:991825827";
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithSender(sender)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            var initContent = await initializeAttachmentResponse.Content.ReadFromJsonAsync<Guid>(_responseSerializerOptions);

            // Act
            var attachmentOverview = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{initContent}");
            var overviewContent = await attachmentOverview.Content.ReadFromJsonAsync<AttachmentOverviewExt>(_responseSerializerOptions);

            // Assert
            Assert.Equal(overviewContent.Sender, $"{UrnConstants.OrganizationNumberAttribute}:{sender.WithoutPrefix()}");
        }
    }
}
