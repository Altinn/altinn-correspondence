using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Legacy.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Legacy
{
    public class LegacyStatusTests : LegacyTestBase
    {
        public LegacyStatusTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_ReturnOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

            // Act
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);
            // Assert
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_CorrespondenceNotExists_Return404()
        {
            // Arrange
            Guid randomCorrespondenceId = Guid.NewGuid();

            // Act and Assert
            var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/confirm", null);
            Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);

            // Act and Assert
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/archive", null);
            Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);

            // Act and Assert
            var readResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/markasread", null);
            Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_CorrespondenceNotPublished_Return404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            Assert.Equal(CorrespondenceStatusExt.ReadyForPublish, correspondence.Status);

            // Act
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_CorrespondencePublished_ReturnOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
            var correspondenceId = correspondence.CorrespondenceId;

            // Act
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/archive", null);
            // Assert
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_MarkAsRead_WithoutFetched_ReturnsBadRequest()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            var correspondenceId = correspondence.CorrespondenceId;

            //  Act
            var readResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/markasread", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, readResponse.StatusCode);
        }
        [Fact]
        public async Task UpdateCorrespondenceStatus_ToConfirmed_WithoutFetched_ReturnsBadRequest()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

            //  Act
            var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_ToConfirmed_WhenCorrespondenceIsFetched_GivesOk()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

            //  Act
            var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_ToArchived_WithoutConfirmation_WhenConfirmationNeeded_ReturnsBadRequest()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                .WithConfirmationNeeded(true)
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

            //  Act
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, archiveResponse.StatusCode);
        }

        [Fact]
        public async Task UpdateCorrespondenceStatus_ToArchived_WithConfirmation_WhenConfirmationNeeded_GivesOk()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                .WithConfirmationNeeded(true)
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);

            //  Act
            var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null); // Update to Confirmed in order to be able to Archive
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
            var archiveResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceOverview_WhenCalledTwice_TransitionsStatusFromPublishedToRead()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            var correspondenceId = correspondence.CorrespondenceId;

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/overview");
            var firstOverview = await response.Content.ReadFromJsonAsync<LegacyCorrespondenceOverviewExt>(_serializerOptions);
            response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/overview");
            var secondOverview = await response.Content.ReadFromJsonAsync<LegacyCorrespondenceOverviewExt>(_serializerOptions);

            // Assert
            Assert.Equal(CorrespondenceStatusExt.Published, firstOverview?.Status);
            Assert.Equal(CorrespondenceStatusExt.Read, secondOverview?.Status);
        }
        [Fact]
        public async Task Can_Get_Overview_When_Purged()
        {
            //  Arrange
            var payload = new CorrespondenceBuilder()
                 .CreateCorrespondence()
                 .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondence.Status);
            var correspondenceId = correspondence.CorrespondenceId;

            var purgeRequest = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}/purge");
            purgeRequest.EnsureSuccessStatusCode();

            var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondenceId}");
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            // Verify the correspondence status after purge
            var purgedCorrespondence = await fetchResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_serializerOptions);
            Assert.Equal(CorrespondenceStatusExt.PurgedByRecipient, purgedCorrespondence?.Status);
        }

    }
}
