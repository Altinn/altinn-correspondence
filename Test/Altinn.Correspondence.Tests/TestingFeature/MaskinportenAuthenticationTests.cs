using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingFeature
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class MaskinportenAuthenticationTests
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly JsonSerializerOptions _responseSerializerOptions;

        // Test constants
        private const string MaskinportenIssuer = "https://test.maskinporten.no/";
        private const string AltinnIssuer = "https://platform.tt02.altinn.no/authentication/api/v1/openid/";
        private const string TestConsumerClaim = "{\"authority\":\"iso6523-actorid-upis\",\"ID\":\"0192:991825827\"}";

        public MaskinportenAuthenticationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _responseSerializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };
            _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }

        #region Helper Methods

        private HttpClient CreateMaskinportenClient(params string[] scopes)
        {
            var scopeString = string.Join(" ", scopes);
            return _factory.CreateClientWithAddedClaims(
                ("scope", scopeString),
                ("iss", MaskinportenIssuer),
                ("consumer", TestConsumerClaim)
            );
        }
        private HttpClient CreateMaskinportenSystemUserClient(params string[] scopes)
        {
            var scopeString = string.Join(" ", scopes);

            var authorizationDetails = new SystemUserAuthorizationDetails()
            {
                Type = UrnConstants.SystemUser,
                SystemUserOrg = new SystemUserOrg()
                {
                    Authority = "iso6523-actorid-upis",
                    ID = "991825827"
                },
                SystemUserId = new List<string>()
                    {
                        Guid.NewGuid().ToString()
                    },
                SystemId = "991825827_correspondencesystem"
            };

            var authDetailsJson = JsonSerializer.Serialize(authorizationDetails);

            return _factory.CreateClientWithAddedClaims(
                ("scope", scopeString),
                ("iss", MaskinportenIssuer),
                ("consumer", TestConsumerClaim),
                ("authorization_details", authDetailsJson)
            );
        }
        private HttpClient CreateValidMaskinportenClient()
        {
            return CreateMaskinportenClient(AuthorizationConstants.ServiceOwnerScope, AuthorizationConstants.SenderScope);
        }

        private HttpClient CreateAltinnClient()
        {
            return _factory.CreateClientWithAddedClaims(
                ("scope", AuthorizationConstants.SenderScope),
                ("iss", AltinnIssuer)
            );
        }

        private static InitializeCorrespondencesExt CreateTestCorrespondence()
        {
            return new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddMinutes(1))
                .Build();
        }

        private static AttachmentBuilder CreateTestAttachment()
        {
            return new AttachmentBuilder().CreateAttachment();
        }

        #endregion

        [Fact]
        public async Task InitializeCorrespondence_WithMaskinportenToken_BothRequiredScopes_Succeeds()
        {
            // Arrange
            var maskinportenClient = CreateValidMaskinportenClient();
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMaskinportenToken_OnlyServiceOwnerScope_Fails()
        {
            // Arrange
            var maskinportenClient = CreateMaskinportenClient(AuthorizationConstants.ServiceOwnerScope);
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMaskinportenToken_OnlyCorrespondenceWriteScope_Fails()
        {
            // Arrange
            var maskinportenClient = CreateMaskinportenClient(AuthorizationConstants.SenderScope);
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMaskinportenToken_NoRequiredScopes_Fails()
        {
            // Arrange
            var maskinportenClient = CreateMaskinportenClient("some:other:scope");
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithAltinnJWTToken_SenderScope_StillWorks()
        {
            // Arrange
            var altinnClient = CreateAltinnClient();
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await altinnClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_WithMaskinportenToken_BothRequiredScopes_Succeeds()
        {
            // Arrange
            var maskinportenClient = CreateValidMaskinportenClient();
            var attachment = CreateTestAttachment().Build();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);

            // Assert
            Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeAttachment_WithMaskinportenToken_MissingScopes_Fails()
        {
            // Arrange
            var maskinportenClient = CreateMaskinportenClient(AuthorizationConstants.ServiceOwnerScope);
            var attachment = CreateTestAttachment().Build();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/attachment", attachment);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task InitializeCorrespondence_WithMaskinportenToken_MultipleScopes_Succeeds()
        {
            // Arrange
            var maskinportenClient = CreateMaskinportenClient(
                "some:other:scope",
                AuthorizationConstants.ServiceOwnerScope,
                AuthorizationConstants.SenderScope,
                "another:scope"
            );
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert
            Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, initializeResponse.StatusCode);
        }

        [Fact]
        public async Task RecipientOperations_WithMaskinportenToken_ServiceOwnerScope_Fails()
        {
            // Arrange - Create a correspondence first
            var senderClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
            var correspondence = CreateTestCorrespondence();

            var initResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            var correspondenceResponse = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = correspondenceResponse.Correspondences.First().CorrespondenceId;

            // Arrange - Create client with Maskinporten token that should not work for recipient operations
            var maskinportenClient = CreateValidMaskinportenClient();

            // Act - Try to perform recipient operation (should fail because Maskinporten tokens are only for sender operations)
            var getResponse = await maskinportenClient.GetAsync($"correspondence/api/v1/correspondence?resourceId=ttd-correspondence-api&status=1&role=recipient");

            // Assert - This should fail as Maskinporten tokens are intended for service owner operations, not recipient operations
            // The recipient policy should still require proper recipient authentication
            Assert.False(getResponse.IsSuccessStatusCode);
        }

        [Fact]
        public async Task MaskinportenAuthentication_ScopeValidation_Works()
        {
            // This test verifies that our authentication policy correctly validates both required scopes
            // for Maskinporten tokens and that it can distinguish between different token types

            // Arrange
            var validMaskinportenClient = CreateValidMaskinportenClient();
            var correspondence = CreateTestCorrespondence();

            // Act & Assert - Valid Maskinporten token should succeed
            var validResponse = await validMaskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            Assert.True(validResponse.IsSuccessStatusCode, "Valid Maskinporten token with both required scopes should succeed");

            // Act & Assert - Traditional Altinn token should still work  
            var altinnClient = CreateAltinnClient();
            var altinnResponse = await altinnClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            Assert.True(altinnResponse.IsSuccessStatusCode, "Traditional Altinn JWT token should still work");
        }

        [Fact]
        public async Task AuthenticationScheme_WithMaskinportenIssuer_ShouldUseMaskinportenScheme()
        {
            // This test verifies that the MockPolicyEvaluator correctly identifies Maskinporten tokens
            // based on the issuer claim and sets the appropriate authentication scheme

            // Arrange
            var maskinportenClient = CreateValidMaskinportenClient();
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

            // Assert - Should succeed, indicating the authentication scheme was correctly identified
            Assert.True(initializeResponse.IsSuccessStatusCode, await initializeResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GetCorrespondenceDetails_WithMaskinportenToken_BothRequiredScopes_Succeeds()
        {
            // Arrange
            var maskinportenClient = CreateValidMaskinportenClient();
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await maskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            var correspondenceResponse = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = correspondenceResponse.Correspondences.First().CorrespondenceId;
            var detailsResponse = await maskinportenClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");

            // Assert
            Assert.True(detailsResponse.IsSuccessStatusCode, await detailsResponse.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GetCorrespondenceDetails_WithMaskinportenToken_OnlyServiceOwnerScope_Fails()
        {
            // Arrange
            var validMaskinportenClient = CreateMaskinportenClient(AuthorizationConstants.SenderScope, AuthorizationConstants.ServiceOwnerScope);
            var invalidMaskinportenClient = CreateMaskinportenClient(AuthorizationConstants.ServiceOwnerScope);
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await validMaskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            var correspondenceResponse = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = correspondenceResponse.Correspondences.First().CorrespondenceId;
            var detailsResponse = await invalidMaskinportenClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, detailsResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceDetails_WithMaskinportenToken_OnlyCorrespondenceWriteScope_Fails()
        {
            // Arrange
            var validMaskinportenClient = CreateMaskinportenClient(AuthorizationConstants.SenderScope, AuthorizationConstants.ServiceOwnerScope);
            var invalidMaskinportenClient = CreateMaskinportenClient(AuthorizationConstants.SenderScope);
            var correspondence = CreateTestCorrespondence();

            // Act
            var initializeResponse = await validMaskinportenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            var correspondenceResponse = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = correspondenceResponse.Correspondences.First().CorrespondenceId;
            var detailsResponse = await invalidMaskinportenClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/details");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, detailsResponse.StatusCode);
        }

        [Fact]
        public async Task GetCorrespondenceContent_WithMaskinportenSystemUserToken_CorrespondenceReadScope_Succeeds()
        {
            // Arrange
            var maskinportenSenderClient = CreateMaskinportenClient(AuthorizationConstants.SenderScope, AuthorizationConstants.ServiceOwnerScope);
            var systemUserRecipientClient = CreateMaskinportenSystemUserClient(AuthorizationConstants.RecipientScope);
            var correspondence = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddMinutes(-1))
                .Build();

            // Act
            var initializeResponse = await maskinportenSenderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);
            var correspondenceResponse = await initializeResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
            var correspondenceId = correspondenceResponse.Correspondences.First().CorrespondenceId;
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(maskinportenSenderClient, _responseSerializerOptions, correspondenceId, CorrespondenceStatusExt.Published);
            var contentResponse = await systemUserRecipientClient.GetAsync($"correspondence/api/v1/correspondence/{correspondenceId}/content");

            // Assert
            Assert.True(contentResponse.IsSuccessStatusCode, await contentResponse.Content.ReadAsStringAsync());
        }
    }
}