using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondences;
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
    public class CorrespondenceSearchTests : CorrespondenceTestBase
    {
        public CorrespondenceSearchTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task GetCorrespondences()
        {
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

            var correspondenceList = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={1}&status={0}&role={"recipientandsender"}");
            Assert.True(correspondenceList?.Ids.Count > 0, string.Join(",", correspondenceList?.Ids ?? []));
        }

        [Fact]
        public async Task GetCorrespondences_WithInvalidRole_ReturnsBadRequest()
        {
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

            var responseWithout = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={1}&status={0}");
            Assert.Equal(HttpStatusCode.BadRequest, responseWithout.StatusCode);

            var responseWithInvalid = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={1}&status={0}&role={"invalid"}");
            Assert.Equal(HttpStatusCode.BadRequest, responseWithInvalid.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondencesOnlyFromSearchedResourceId()
        {
            var resourceA = Guid.NewGuid().ToString();
            var resourceB = Guid.NewGuid().ToString();
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _responseSerializerOptions);

            var payloadResourceA = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithResourceId(resourceA)
                .Build();
            var payloadResourceB = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithExistingAttachments([attachmentId])
                .WithResourceId(resourceB)
                .Build();

            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadResourceA);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());

            var initializeCorrespondenceResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadResourceB);
            Assert.True(initializeCorrespondenceResponse2.IsSuccessStatusCode, await initializeCorrespondenceResponse2.Content.ReadAsStringAsync());

            int status = (int)CorrespondenceStatusExt.Published;
            var correspondenceListResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={resourceA}&status={status}&role={"recipientandsender"}");
            Assert.True(correspondenceListResponse.IsSuccessStatusCode, await correspondenceListResponse.Content.ReadAsStringAsync());
            var correspondenceList = await correspondenceListResponse.Content.ReadFromJsonAsync<GetCorrespondencesResponse>(_responseSerializerOptions);
            Assert.True(correspondenceList?.Ids.Count > 0, "No correspondence found");
            Assert.Equal(payloadResourceA.Recipients.Count, correspondenceList?.Ids.Count);
        }

        [Fact]
        public async Task GetCorrespondences_When_IsSender_Or_IsRecipient_Specified_ReturnsSpecifiedCorrespondences()
        {
            // Arrange
            var resource = Guid.NewGuid().ToString();
            var recipientId = $"{UrnConstants.OrganizationNumberAttribute}:000000000";
            var senderId = $"{UrnConstants.OrganizationNumberAttribute}:111111111";
            var externalId = $"{UrnConstants.OrganizationNumberAttribute}:222222222";

            // Create correspondence as Sender with recipientId amongst recipients
            var senderPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resource)
                .WithSender(senderId)
                .WithRecipients([recipientId, $"{UrnConstants.OrganizationNumberAttribute}:123456789", $"{UrnConstants.OrganizationNumberAttribute}:321654987"])
                .Build();
            var senderClient = _factory.CreateClientWithAddedClaims(
                ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{senderId}\"}}"),
                ("notRecipient", "true"),
                ("scope", AuthorizationConstants.SenderScope)
            );
            var initResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", senderPayload);
            Assert.True(initResponse.IsSuccessStatusCode, await initResponse.Content.ReadAsStringAsync());

            // Create some correspondences with External sender with senderId and recipientId amongst recipients
            var externalPayload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resource)
                .WithSender(externalId)
                .WithRecipients([senderId, recipientId, $"{UrnConstants.OrganizationNumberAttribute}:864231509"])
                .Build();
            var externalClient = _factory.CreateClientWithAddedClaims(
                ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{externalId}\"}}"),
                ("notRecipient", "true"),
                ("scope", AuthorizationConstants.SenderScope)
            );
            var externalInitResponse = await externalClient.PostAsJsonAsync("correspondence/api/v1/correspondence", externalPayload);
            Assert.True(externalInitResponse.IsSuccessStatusCode, await externalInitResponse.Content.ReadAsStringAsync());

            // Create recipient client to retrieve correspondences with correct ID
            var recipientIdClient = _factory.CreateClientWithAddedClaims(
                ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{recipientId}\"}}"),
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope)
            );

            // Act
            var correspondencesSender = await senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&role={"sender"}");
            var correspondencesRecipient = await recipientIdClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&role={"recipient"}");
            var correspondencesSenderAndRecipient = await senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&role={"recipientandsender"}");

            // Assert
            var expectedSender = senderPayload.Recipients.Count; // sender only sees the ones they sent
            Assert.Equal(3, expectedSender);
            var expectedRecipient = senderPayload.Recipients.Where(r => r == recipientId).Count() + externalPayload.Recipients.Where(r => r == recipientId).Count(); // recipient sees the ones from the initial sender and external sender
            Assert.Equal(2, expectedRecipient);
            var expectedSenderAndRecipient = expectedSender + externalPayload.Recipients.Where(r => r == senderId).Count(); // sender sees the ones they sent and the ones where they were the recipient from external
            Assert.Equal(expectedSenderAndRecipient, correspondencesSenderAndRecipient?.Ids.Count);
        }

        [Fact]
        public async Task GetCorrespondences_WithStatusSpecified_ShowsSpecifiedCorrespondences()
        {
            // Arrange
            var resourceId = Guid.NewGuid().ToString();
            var initializedCorrespondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resourceId)
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var initializeResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", initializedCorrespondence);
            var correspondence = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var getCorrespondenceOverviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence?.Correspondences.FirstOrDefault().CorrespondenceId}");
            var b = await getCorrespondenceOverviewResponse.Content.ReadFromJsonAsync<CorrespondenceOverviewExt>(_responseSerializerOptions);

            var publishedCorrespondences = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resourceId)
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(-1))
                .Build();
            await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", publishedCorrespondences);

            // Act
            var responseWithReadyForPublish = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&status={(int)CorrespondenceStatusExt.ReadyForPublish}&role={"sender"}");
            var responseWithPublished = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resourceId}&status={(int)CorrespondenceStatusExt.Published}&role={"sender"}");

            // Assert
            var expectedInitialized = initializedCorrespondence.Recipients.Count;
            Assert.Equal(expectedInitialized, responseWithReadyForPublish?.Ids.Count);
            var expectedPublished = publishedCorrespondences.Recipients.Count;
            Assert.Equal(expectedPublished, responseWithPublished?.Ids.Count);
        }

        [Fact]
        public async Task GetCorrespondences_WithoutStatusSpecified_AsReceiver_ReturnsAllExceptBlacklisted()
        {
            // Arrange
            var resource = Guid.NewGuid().ToString();
            var recipientId = $"{UrnConstants.OrganizationNumberAttribute}:000000000";
            var recipientClient = _factory.CreateClientWithAddedClaims(
                ("consumer", $"{{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"{recipientId}\"}}"),
                ("notSender", "true"),
                ("scope", AuthorizationConstants.RecipientScope)
            );

            var payloadPublished = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resource)
                .WithRecipients([recipientId])
                .Build(); // One published

            var payloadReadyForPublish = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resource)
                .WithRecipients([recipientId])
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build(); // One ready for publish

            // Act
            var responsePublished = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadPublished);
            Assert.True(responsePublished.IsSuccessStatusCode, await responsePublished.Content.ReadAsStringAsync());
            var responsePublishedContent = await responsePublished.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var responseReadyForPublish = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payloadReadyForPublish);
            Assert.True(responseReadyForPublish.IsSuccessStatusCode, await responseReadyForPublish.Content.ReadAsStringAsync());
            var correspondenceList = await recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&role={"recipient"}");

            // Assert
            var expected = payloadPublished.Recipients.Where(r => r == recipientId).Count(); // Receiver only sees the one that is published
            Assert.Equal(expected, correspondenceList?.Ids.Count);
        }

        [Fact]
        public async Task GetCorrespondences_WithoutStatusSpecified_AsSender_ReturnsAllExceptBlacklisted()
        {
            // Arrange
            var resource = Guid.NewGuid().ToString();
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resource)
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build(); // One ReadyForPublish

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
            var correspondencesBeforeDeletion = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&role={"sender"}");
            var response = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);

            await _senderClient.DeleteAsync($"correspondence/api/v1/correspondence/{response.Correspondences.FirstOrDefault().CorrespondenceId}/purge");
            var correspondencesAfterDeletion = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&role={"sender"}");

            // Assert
            var expectedBeforeDeletion = payload.Recipients.Count;
            Assert.Equal(correspondencesBeforeDeletion?.Ids.Count, expectedBeforeDeletion);
            var expectedAfterDeletion = payload.Recipients.Count - 1; // One was deleted
            Assert.Equal(expectedAfterDeletion, correspondencesAfterDeletion?.Ids.Count);
        }

        [Fact]
        public async Task GetCorrespondences_WithStatusSpecified_ButStatusIsBlackListed_DoesNotReturnCorrespondence()
        {
            // Arrange
            var resource = Guid.NewGuid().ToString();
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resource)
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(5))
                .Build(); // One ReadyForPublish

            // Act
            var initializeCorrespondenceResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initializeCorrespondenceResponse.IsSuccessStatusCode, await initializeCorrespondenceResponse.Content.ReadAsStringAsync());
            var correspondencesRecipient = await _recipientClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&status={1}&role={"recipient"}");
            var correspondencesSender = await _senderClient.GetFromJsonAsync<GetCorrespondencesResponse>($"correspondence/api/v1/correspondence?resourceId={resource}&status={1}&role={"sender"}");

            // Assert
            var expectedRecipient = 0; // recipient does not see ReadyForPublish
            var expectedSender = payload.Recipients.Count;
            Assert.Equal(expectedRecipient, correspondencesRecipient?.Ids.Count);
            Assert.Equal(expectedSender, correspondencesSender?.Ids.Count);
        }

        [Fact]
        public async Task GetCorrespondences_With_Invalid_Date_Gives_BadRequest()
        {
            var response = await _senderClient.GetAsync($"correspondence/api/v1/correspondence?resourceId={1}&status={0}&role={"recipientandsender"}&from={DateTimeOffset.UtcNow.AddDays(1)}&to={DateTimeOffset.UtcNow}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondences_WithSendersReference_ReturnsMatchingCorrespondences()
        {
            // Arrange
            var resourceId = Guid.NewGuid().ToString();
            var sendersReference = "test-ref-123";
            
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resourceId)
                .WithSendersReference(sendersReference)
                .Build();
            
            var initResponse = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
            Assert.True(initResponse.IsSuccessStatusCode, await initResponse.Content.ReadAsStringAsync());
            var initResult = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var expectedCorrespondenceId = initResult.Correspondences.First().CorrespondenceId;
            
            // Act
            var searchResponse = await _senderClient.GetAsync($"/correspondence/api/v1/correspondence?resourceId={resourceId}&sendersReference={sendersReference}&role={"recipientandsender"}");
            
            // Assert
            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
            var result = await searchResponse.Content.ReadFromJsonAsync<GetCorrespondencesResponse>(_responseSerializerOptions);
            Assert.Single(result.Ids);
            Assert.Equal(expectedCorrespondenceId, result.Ids.First());
        }

        [Fact]
        public async Task GetCorrespondences_WithDifferentSendersReferences_ReturnsCorrectCorrespondences()
        {
            // Arrange
            var resourceId = Guid.NewGuid().ToString();
            var ref1 = "ref-1";
            var ref2 = "ref-2";
            
            // Create two correspondences with different sendersReferences
            var payload1 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resourceId)
                .WithSendersReference(ref1)
                .Build();
            
            var payload2 = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithResourceId(resourceId)
                .WithSendersReference(ref2)
                .Build();
            
            var initResponse1 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload1);
            Assert.True(initResponse1.IsSuccessStatusCode, await initResponse1.Content.ReadAsStringAsync());
            var initResult1 = await initResponse1.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var expectedCorrespondenceId1 = initResult1.Correspondences.First().CorrespondenceId;
            
            var initResponse2 = await _senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", payload2);
            Assert.True(initResponse2.IsSuccessStatusCode, await initResponse2.Content.ReadAsStringAsync());
            var initResult2 = await initResponse2.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var expectedCorrespondenceId2 = initResult2.Correspondences.First().CorrespondenceId;
            
            // Act & Assert for ref1
            var search1 = await _senderClient.GetAsync($"/correspondence/api/v1/correspondence?resourceId={resourceId}&sendersReference={ref1}&role={"recipientandsender"}");
            var result1 = await search1.Content.ReadFromJsonAsync<GetCorrespondencesResponse>(_responseSerializerOptions);
            Assert.Single(result1.Ids);
            Assert.Equal(expectedCorrespondenceId1, result1.Ids.First());
            
            // Act & Assert for ref2
            var search2 = await _senderClient.GetAsync($"/correspondence/api/v1/correspondence?resourceId={resourceId}&sendersReference={ref2}&role={"recipientandsender"}");
            var result2 = await search2.Content.ReadFromJsonAsync<GetCorrespondencesResponse>(_responseSerializerOptions);
            Assert.Single(result2.Ids);
            Assert.Equal(expectedCorrespondenceId2, result2.Ids.First());
            
            // Act & Assert for non-existent reference
            var search3 = await _senderClient.GetAsync($"/correspondence/api/v1/correspondence?resourceId={resourceId}&sendersReference=non-existent&role={"recipientandsender"}");
            var result3 = await search3.Content.ReadFromJsonAsync<GetCorrespondencesResponse>(_responseSerializerOptions);
            Assert.Empty(result3.Ids);
            
            // Act & Assert for all correspondences (without sendersReference)
            var search4 = await _senderClient.GetAsync($"/correspondence/api/v1/correspondence?resourceId={resourceId}&role={"recipientandsender"}");
            var result4 = await search4.Content.ReadFromJsonAsync<GetCorrespondencesResponse>(_responseSerializerOptions);
            Assert.Equal(2, result4.Ids.Count);
            Assert.Contains(expectedCorrespondenceId1, result4.Ids);
            Assert.Contains(expectedCorrespondenceId2, result4.Ids);
        }
    }
}
