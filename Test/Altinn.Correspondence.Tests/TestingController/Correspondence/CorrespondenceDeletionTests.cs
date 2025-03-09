using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceDeletionTests : CorrespondenceTestBase
    {
        public CorrespondenceDeletionTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task Delete_ReadyForPublished_Correspondence_SuccessForSender_FailsForRecipient()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(correspondenceResponse);
            var correspondenceId = correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId;

            // Verify correspondence exists before deletion
            var getResponseBeforePurge = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.Equal(HttpStatusCode.OK, getResponseBeforePurge.StatusCode);

            // Act (Call recipient first to ensure that the correspondence is not purged)
            var recipientResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
            var senderResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, recipientResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, senderResponse.StatusCode);

            // Since we can no longer check the status directly after purging, we'll verify by confirming
            // that attempting to get the correspondence now returns a 404 (Not Found)
            var getResponseAfterPurge = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.Equal(HttpStatusCode.NotFound, getResponseAfterPurge.StatusCode);
        }

        [Fact]
        public async Task Delete_Published_Correspondence_SuccessForRecipient_FailsForSender()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(correspondenceResponse);
            var correspondenceId = correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId;

            // Verify correspondence exists before deletion
            var getResponseBeforePurge = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.Equal(HttpStatusCode.OK, getResponseBeforePurge.StatusCode);

            // Act (Call sender first to ensure that the correspondence is not purged)
            var senderResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
            var recipientResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, senderResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, recipientResponse.StatusCode);

            // Since we can no longer check the status directly after purging, we'll verify by confirming
            // that attempting to get the correspondence now returns a 404 (Not Found)
            var getResponseAfterPurge = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.Equal(HttpStatusCode.NotFound, getResponseAfterPurge.StatusCode);
        }

        [Fact]
        public async Task Delete_Published_Correspondence_WithoutConfirmation_WhenConfirmationNeeded_ReturnsBadRequest()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                .WithConfirmationNeeded(true)
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
            var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

            //  Act
            var deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_Published_Correspondence_WithConfirmation_WhenConfirmationNeeded_Gives_OK()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                .WithConfirmationNeeded(true)
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondenceResponse?.Correspondences?.FirstOrDefault()?.Status);
            var correspondenceId = correspondenceResponse?.Correspondences?.FirstOrDefault()?.CorrespondenceId;

            //  Act
            var fetchResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}"); // Fetch in order to be able to confirm
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            var confirmResponse = await _recipientClient.PostAsync($"correspondence/api/v1/correspondence/{correspondenceId}/confirm", null); // Confirm in order to be able to delete
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
            var deleteResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_Correspondence_Also_deletes_attachment()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var correspondenceResponse = await CreateCorrespondenceWithAttachment(attachmentId, DateTimeOffset.UtcNow.AddDays(1));
            Assert.NotNull(correspondenceResponse);

            // Store all correspondenceIds for later verification
            var correspondenceIds = correspondenceResponse.Correspondences.Select(c => c.CorrespondenceId).ToList();

            // Act - delete all correspondences
            foreach (var correspondenceId in correspondenceIds)
            {
                var response = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                // Verify correspondence is not found after deletion
                var getResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
                Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
            }

            // Assert - Verify attachment status indirectly (attempt to download should fail)
            var attachmentDownloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{correspondenceResponse.AttachmentIds.FirstOrDefault()}/content");
            Assert.Equal(HttpStatusCode.NotFound, attachmentDownloadResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_correspondence_dont_delete_attachment_with_multiple_correspondences()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var initializeCorrespondenceResponse1 = await CreateCorrespondenceWithAttachment(attachmentId, DateTimeOffset.UtcNow.AddDays(1));
            var initializeCorrespondenceResponse2 = await CreateCorrespondenceWithAttachment(attachmentId, DateTimeOffset.UtcNow.AddDays(1));

            // Delete the first correspondence
            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{initializeCorrespondenceResponse1.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            // The attachment should still exist and be accessible since it's used by the second correspondence
            var attachmentDownloadResponse = await _senderClient.GetAsync($"correspondence/api/v1/attachment/{attachmentId}");
            Assert.Equal(HttpStatusCode.OK, attachmentDownloadResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_NonExisting_Correspondence_Gives_NotFound()
        {
            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/purge");
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }

        private async Task<InitializeCorrespondencesResponseExt> CreateCorrespondenceWithAttachment(
            Guid attachmentId,
            DateTimeOffset publishTime)
        {
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRequestedPublishTime(publishTime)
                .Build();

            var response = await _senderClient.PostAsJsonAsync(
                "correspondence/api/v1/correspondence",
                payload,
                _responseSerializerOptions);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        }
    }
}