using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using System.Net.Http.Json;
using System.Net;
using Altinn.Correspondence.Tests.Fixtures;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
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
        [Theory]
        [InlineData("rsietris//rsitersn")]
        [InlineData("    ")]
        public async Task InitializeAttachment_InvalidFilename_ReturnsBadRequest(string fileName)
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(fileName)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_FileNameTooLong_ReturnsBadRequest()
        {
            string namewith300chars = new string('a', 300);
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithFileName(namewith300chars)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_DisplayFileNameTooLong_ReturnsBadRequest()
        {
            string namewith300chars = new string('a', 300);
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithDisplayName(namewith300chars)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.BadRequest, initializeAttachmentResponse.StatusCode);
        }
        [Fact]
        public async Task InitializeAttachment_DisplayFileNameIsNull_Returns_OK()
        {
            var attachment = new AttachmentBuilder()
                .CreateAttachment()
                .WithDisplayName(null)
                .Build();
            var initializeAttachmentResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);
            Assert.Equal(HttpStatusCode.OK, initializeAttachmentResponse.StatusCode);
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
    }
}
