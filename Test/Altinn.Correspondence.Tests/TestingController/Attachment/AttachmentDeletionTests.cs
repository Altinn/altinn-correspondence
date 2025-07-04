using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Attachment.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Attachment
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class AttachmentDeletionTests : AttachmentTestBase
    {
        public AttachmentDeletionTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }
        [Fact]
        public async Task DeleteAttachment_WhenAttachmentDoesNotExist_ReturnsNotFound()
        {
            var deleteResponse = await _senderClient.DeleteAsync("correspondence/api/v1/attachment/00000000-0100-0000-0000-000000000000");
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteAttachment_WhenAttachmentIsIntiliazied_Succeeds()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);

            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.True(deleteResponse.IsSuccessStatusCode, await deleteResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task DeleteAttachment_Twice_Fails()
        {
            var attachmentId = await AttachmentHelper.GetInitializedAttachment(_senderClient, _responseSerializerOptions);

            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.True(deleteResponse.IsSuccessStatusCode, await deleteResponse.Content.ReadAsStringAsync());

            var deleteResponse2 = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.Equal(HttpStatusCode.BadRequest, deleteResponse2.StatusCode);
        }

        [Fact]
        public async Task DeleteAttachment_WhenAttachedCorrespondenceIsPublished_ReturnsBadRequest()
        {
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteAttachment_WhenAttachedCorrespondenceIsReadyForPublish_Fails()
        {
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRequestedPublishTime(DateTime.UtcNow.AddDays(1))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse?.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
            Assert.True(overview?.Status == CorrespondenceStatusExt.ReadyForPublish);

            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/attachment/{correspondenceResponse?.AttachmentIds.FirstOrDefault()}");
            Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteAttachment_AsRecipient_ReturnsForbidden()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            // Assert failure before correspondence is created
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Act
            deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            // Assert 
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteAttachment_AsWrongSender_ReturnsUnauthorized()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var deleteResponse = await _wrongSenderClient.DeleteAsync($"correspondence/api/v1/attachment/{attachmentId}");
            // Assert failure before correspondence is created
            Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
        }
    }
}
