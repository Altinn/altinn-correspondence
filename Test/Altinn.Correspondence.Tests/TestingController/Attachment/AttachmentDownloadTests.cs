using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    public class AttachmentDownloadTests : AttachmentTestBase
    {
        public AttachmentDownloadTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task DownloadAttachment_AsRecipient_ReturnsForbidden()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            // Act
            var downloadResponse = await _recipientClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
            var data = await downloadResponse.Content.ReadAsByteArrayAsync();

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, downloadResponse.StatusCode);
            Assert.Empty(data);
        }
        [Fact]
        public async Task DownloadAttachment_AsWrongSender_ReturnsBadRequest()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            // Act
            var downloadResponse = await _wrongSenderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
            var data = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetails>();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
            Assert.NotNull(data?.Title);
        }
    }
}
