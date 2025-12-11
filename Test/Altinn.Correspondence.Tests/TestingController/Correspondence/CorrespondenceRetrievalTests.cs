using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Correspondence.Base;
using System.Net;
using System.Net.Http;
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
            var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence.Correspondences.FirstOrDefault().CorrespondenceId}");

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
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var getCorrespondenceOverviewResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
            Assert.Equal(CorrespondenceStatusExt.Published, response!.Status); // Status is not changed to fetched
            var actual = await (await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details")).Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

            var expectedFetchedStatuses = 1;
            Assert.Equal(actual!.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
            Assert.Contains(actual.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
        }

        [Fact]
        public async Task GetCorrespondenceOverview_AsSender_KeepsStatusAsPublished()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.True(getCorrespondenceOverviewResponse.IsSuccessStatusCode, await getCorrespondenceOverviewResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);
            var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            var detailsResponse = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            Assert.DoesNotContain(detailsResponse!.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched); // Fetched is not added to the list
            Assert.Equal(CorrespondenceStatusExt.Published, response!.Status);
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
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var getCorrespondenceDetailsResponse = await _recipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            var expectedFetchedStatuses = 1;
            Assert.Equal(response!.StatusHistory.Where(s => s.Status == CorrespondenceStatusExt.Fetched).Count(), expectedFetchedStatuses);
            Assert.Contains(response!.StatusHistory, item => item.Status == CorrespondenceStatusExt.Published);
            Assert.Equal(CorrespondenceStatusExt.Published, response!.Status);
        }

        [Fact]
        public async Task GetCorrespondenceDetails_AsSender_KeepsStatusAsPublished()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            Assert.True(getCorrespondenceDetailsResponse.IsSuccessStatusCode, await getCorrespondenceDetailsResponse.Content.ReadAsStringAsync());

            // Assert
            var response = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            Assert.DoesNotContain(response!.StatusHistory, item => item.Status == CorrespondenceStatusExt.Fetched);
            Assert.Equal(CorrespondenceStatusExt.Published, response.Status);
        }

        [Fact]
        public async Task PersonalCorrespondence_RetrievableWithRecipientPersonalToken()
        {
            // Arrange
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRecipients(["26818099001"])
                .Build();

            // Act
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, correspondence);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);
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
                .WithRecipients(["26818099001"])
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

        [Fact]
        public async Task CorrespondenceIsPurgedByRecipient_StatusNotVisibleForSender()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Purge the correspondence
            var purgeResponse = await _recipientClient.DeleteAsync($"correspondence/api/v1/correspondence/{correspondenceId}/purge");
            purgeResponse.EnsureSuccessStatusCode();

            // Act
            // Try to access the correspondence as sender
            var getCorrespondenceDetailsResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            var detailsResponse = await getCorrespondenceDetailsResponse.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);

            // Assert
            Assert.DoesNotContain(detailsResponse!.StatusHistory, statusEntity => statusEntity.Status == CorrespondenceStatusExt.PurgedByRecipient);
        }

        [Fact]
        public async Task CorrespondencePublished_ContentNullForSender()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            // Act
            var correspondenceDetails = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            var correspondenceOverview = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.True(correspondenceDetails.IsSuccessStatusCode, await correspondenceDetails.Content.ReadAsStringAsync());
            var correspondenceDetailsResponse = await correspondenceDetails.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            Assert.True(correspondenceOverview.IsSuccessStatusCode, await correspondenceOverview.Content.ReadAsStringAsync());
            var correspondenceOverviewResponse = await correspondenceOverview.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);

            // Assert
            Assert.NotNull(correspondenceDetailsResponse);
            Assert.NotNull(correspondenceOverviewResponse);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondenceDetailsResponse.Status);
            Assert.Equal(CorrespondenceStatusExt.Published, correspondenceOverviewResponse.Status);
            Assert.Empty(correspondenceDetailsResponse.Content!.MessageSummary);
            Assert.Empty(correspondenceDetailsResponse.Content.MessageBody);
            Assert.Empty(correspondenceDetailsResponse.Content.MessageTitle);
            Assert.Empty(correspondenceOverviewResponse.Content!.MessageSummary);
            Assert.Empty(correspondenceOverviewResponse.Content.MessageBody);
            Assert.Empty(correspondenceOverviewResponse.Content.MessageTitle);

        }

        [Fact]
        public async Task CorrespondenceNotPublished_ContentNotNullForSender()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            var correspondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var initializedCorrespondence = correspondence!.Correspondences.First();
            var correspondenceId = initializedCorrespondence.CorrespondenceId;

            // Act
            var correspondenceDetails = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");
            var correspondenceOverview = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}");
            Assert.True(correspondenceDetails.IsSuccessStatusCode, await correspondenceDetails.Content.ReadAsStringAsync());
            var correspondenceDetailsResponse = await correspondenceDetails.Content.ReadFromJsonAsync<CorrespondenceDetailsExt>(_responseSerializerOptions);
            Assert.True(correspondenceOverview.IsSuccessStatusCode, await correspondenceOverview.Content.ReadAsStringAsync());
            var correspondenceOverviewResponse = await correspondenceOverview.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);

            // Assert
            Assert.NotNull(correspondenceDetailsResponse);
            Assert.NotNull(correspondenceOverviewResponse);
            Assert.NotEqual(CorrespondenceStatusExt.Published, correspondenceDetailsResponse.Status);
            Assert.NotEqual(CorrespondenceStatusExt.Published, correspondenceOverviewResponse.Status);
            Assert.NotEmpty(correspondenceDetailsResponse.Content!.MessageSummary);
            Assert.NotEmpty(correspondenceDetailsResponse.Content.MessageBody);
            Assert.NotEmpty(correspondenceDetailsResponse.Content.MessageTitle);
            Assert.NotEmpty(correspondenceOverviewResponse.Content!.MessageSummary);
            Assert.NotEmpty(correspondenceOverviewResponse.Content.MessageBody);
            Assert.NotEmpty(correspondenceOverviewResponse.Content.MessageTitle);
        }

        [Fact]
        public async Task GetCorrespondenceContent_WithAllowedOrigin_ReturnsCorsHeaders()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            var client = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));
            client.DefaultRequestHeaders.Add("Origin", "https://af.tt02.altinn.no");

            // Act
            var response = await client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/content");

            // Assert
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"), "CORS header Access-Control-Allow-Origin should be present");
            Assert.Equal("https://af.tt02.altinn.no", response.Headers.GetValues("Access-Control-Allow-Origin").First());
            Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"), "CORS header Access-Control-Allow-Credentials should be present");
            Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").First());
        }

        [Fact]
        public async Task GetCorrespondenceContent_WithSecondAllowedOrigin_ReturnsCorsHeaders()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            var client = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));
            client.DefaultRequestHeaders.Add("Origin", "http://af.tt.altinn.no");

            // Act
            var response = await client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/content");

            // Assert
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"), "CORS header Access-Control-Allow-Origin should be present");
            Assert.Equal("http://af.tt.altinn.no", response.Headers.GetValues("Access-Control-Allow-Origin").First());
            Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"), "CORS header Access-Control-Allow-Credentials should be present");
            Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").First());
        }

        [Fact]
        public async Task GetCorrespondenceContent_WithDisallowedOrigin_DoesNotReturnCorsHeaders()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            var client = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));
            client.DefaultRequestHeaders.Add("Origin", "https://malicious-site.com");

            // Act
            var response = await client.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/content");

            // Assert
            // The request may still succeed (200) if authentication passes, but CORS headers should not be present
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"), "CORS header Access-Control-Allow-Origin should not be present for disallowed origin");
        }

        [Fact]
        public async Task GetCorrespondenceContent_OptionsPreflight_WithAllowedOrigin_ReturnsCorsHeaders()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            var client = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));
            client.DefaultRequestHeaders.Add("Origin", "https://af.tt02.altinn.no");
            client.DefaultRequestHeaders.Add("Access-Control-Request-Method", "GET");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Options, $"correspondence/api/v1/correspondence/{correspondenceId}/content");
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"), "CORS header Access-Control-Allow-Origin should be present");
            Assert.Equal("https://af.tt02.altinn.no", response.Headers.GetValues("Access-Control-Allow-Origin").First());
            Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"), "CORS header Access-Control-Allow-Credentials should be present");
            Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").First());
            Assert.True(response.Headers.Contains("Access-Control-Allow-Methods"), "CORS header Access-Control-Allow-Methods should be present");
        }

        [Fact]
        public async Task GetCorrespondenceContent_OptionsPreflight_WithDisallowedOrigin_DoesNotReturnCorsHeaders()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializedCorrespondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _responseSerializerOptions, payload);
            var correspondenceId = initializedCorrespondence.CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);

            var client = _factory.CreateClientWithAddedClaims(
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope));
            client.DefaultRequestHeaders.Add("Origin", "https://malicious-site.com");
            client.DefaultRequestHeaders.Add("Access-Control-Request-Method", "GET");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Options, $"correspondence/api/v1/correspondence/{correspondenceId}/content");
            var response = await client.SendAsync(request);

            // Assert
            // Preflight requests from disallowed origins should not return CORS headers
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"), "CORS header Access-Control-Allow-Origin should not be present for disallowed origin");
        }
    }
}
