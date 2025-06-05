using Altinn.Correspondence.Application;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
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
            Assert.Equal(HttpStatusCode.Unauthorized, downloadResponse.StatusCode);
            Assert.NotNull(data?.Title);
        }

        [Fact]
        public async Task DownloadAttachment_AsSenderAfterAttachedToPublishedCorrespondence_Fails()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var correspondencePayload = new CorrespondenceBuilder().CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();

            // Act
            var downloadResponseBeforeAttached = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");
            Assert.True(downloadResponseBeforeAttached.IsSuccessStatusCode, await downloadResponseBeforeAttached.Content.ReadAsStringAsync());
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondencePayload, CancellationToken.None);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
            var downloadResponseAfterAttached = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}/download");

            // Assert
            Assert.True(downloadResponseAfterAttached.StatusCode == HttpStatusCode.BadRequest, await downloadResponseAfterAttached.Content.ReadAsStringAsync());
            var data = await downloadResponseAfterAttached.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(data.Detail, AttachmentErrors.AttachedToAPublishedCorrespondence.Message);
        }
    }
}
