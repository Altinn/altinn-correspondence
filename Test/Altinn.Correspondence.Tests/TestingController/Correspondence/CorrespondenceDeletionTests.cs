using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
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

            // Act (Call recipient first to ensure that the correspondence is not purged)
            var recipientResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
            var senderResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, recipientResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, senderResponse.StatusCode);
            var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
            Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByAltinn);
        }

        [Fact]
        public async Task Delete_Published_Correspondence_SuccessForRecipient_FailsForSender()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(correspondenceResponse);

            // Act (Call sender first to ensure that the correspondence is not purged)
            var senderResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
            var recipientResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, senderResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, recipientResponse.StatusCode);
            var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceResponse.Correspondences.FirstOrDefault().CorrespondenceId}", _responseSerializerOptions);
            Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByRecipient);
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
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondenceResponse = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            Assert.NotNull(correspondenceResponse);

            // Act
            foreach (var correspondence in correspondenceResponse.Correspondences)
            {
                var correspondenceId = correspondence.CorrespondenceId;
                var response = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var overview = await _senderClient.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", _responseSerializerOptions);
                Assert.Equal(overview?.Status, CorrespondenceStatusExt.PurgedByAltinn);
            }

            // Assert
            var attachment = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{correspondenceResponse.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
            Assert.Equal(attachment?.Status, AttachmentStatusExt.Purged);
        }

        [Fact]
        public async Task Delete_correspondence_dont_delete_attachment_with_multiple_correspondences()
        {
            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);
            var payload1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();

            var initializeCorrespondenceResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1, _responseSerializerOptions);
            var response1 = await initializeCorrespondenceResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse1.EnsureSuccessStatusCode();
            Assert.NotNull(response1);

            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2, _responseSerializerOptions);
            var response2 = await initializeCorrespondenceResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            initializeCorrespondenceResponse2.EnsureSuccessStatusCode();
            Assert.NotNull(response2);

            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{response1.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var attachmentOverview = await _senderClient.GetFromJsonAsync<AttachmentOverviewExt>($"correspondence/api/v1/attachment/{response1.AttachmentIds.FirstOrDefault()}", _responseSerializerOptions);
            Assert.NotEqual(attachmentOverview?.Status, AttachmentStatusExt.Purged);
        }

        [Fact]
        public async Task Delete_NonExisting_Correspondence_Gives_NotFound()
        {
            var deleteResponse = await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/00000000-0100-0000-0000-000000000000/purge");
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }
    }
}
