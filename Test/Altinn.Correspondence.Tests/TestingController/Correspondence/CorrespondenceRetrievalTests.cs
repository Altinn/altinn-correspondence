using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Correspondence
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class CorrespondenceRetrievalTests : CorrespondenceTestBase
    {
        public CorrespondenceRetrievalTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task GetCorrespondenceOverview()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, getCorrespondenceOverviewResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceOverview_WhenNotSenderOrRecipient_Returns401()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var invalidClient = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("notRecipient", "true"),
                ("scope", AuthorizationConstants.SenderScope));

            // Act
            var invalidSenderResponse = await invalidClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, invalidSenderResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceOverview_AsReceiver_WhenNotPublishedReturns404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceOverviewResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceOverview_AsReceiver_AddsFetchedStatusToHistory()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
            Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
            Assert.Equal(CorrespondenceStatusExt.Published, response.Status); // Status is not changed to fetched
            var actual = await (await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details")).Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

            var expectedFetchedStatuses = 1;
            Assert.Equal(actual.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
            Assert.Contains(actual.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
        }

        [Fact]
        public async Task GetCorrespondenceOverview_AsSender_KeepsStatusAsPublished()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
            Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
            var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
            var detailsResponse = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            Assert.DoesNotContain(detailsResponse.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched); // Fetched is not added to the list
            Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
        }

        [Fact]
        public async Task GetCorrespondenceDetails()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");

            // Assert
            Assert.Equal(HttpStatusCode.OK, getCorrespondenceDetailsResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceDetails_WhenNotSenderOrRecipient_Returns401()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var invalidClient = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("notRecipient", "true"),
                ("scope", AuthorizationConstants.SenderScope));

            // Act
            var invalidResponse = await invalidClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceDetails_AsReceiver_WhenNotPublishedReturns404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, getCorrespondenceDetailsResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceDetails_AsReceiver_AddsFetchedStatusToHistory()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
            Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            var expectedFetchedStatuses = 1;
            Assert.Equal(response.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
            Assert.Contains(response.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
            Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
        }

        [Fact]
        public async Task GetCorrespondenceDetails_AsSender_KeepsStatusAsPublished()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            initializeCorrespondenceResponse.EnsureSuccessStatusCode();
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            // Act
            var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}/details");
            Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            Assert.DoesNotContain(response.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched);
            Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
        }

        [Fact]
        public async Task PersonalCorrespondence_RetrievableWithRecipientPersonalToken()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["11015699332"])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
            var initializedCorrespondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = initializedCorrespondence?.Correspondences.FirstOrDefault().CorrespondenceId;
            var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            var getCorrespondenceContentResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/content");

            // Assert
            Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());
            Assert.True(getCorrespondenceContentResponse.IsSuccessStatusCode, await getCorrespondenceContentResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task PersonalCorrespondence_NotRetrievableWithWrongRecipientToken()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["11015699332"])
                .Build();

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            var initializedCorrespondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = initializedCorrespondence?.Correspondences.FirstOrDefault().CorrespondenceId;

            var wrongRecipientClient = _factory.CreateClientWithAddedClaims(
                ("pid", "wrong-personal-id"),
                ("notRecipient", "true"),
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));

            var getCorrespondenceResponse = await wrongRecipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, getCorrespondenceResponse.StatusCode);
        }
    }
}
