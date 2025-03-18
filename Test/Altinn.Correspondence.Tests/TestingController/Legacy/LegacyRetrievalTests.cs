using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Legacy.Base;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Altinn.Correspondence.Core.Services;
using Moq;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Tests.Fixtures;

namespace Altinn.Correspondence.Tests.TestingController.Legacy
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class LegacyRetrievalTests : LegacyTestBase
    {
        public LegacyRetrievalTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }
        [Fact]
        public async Task LegacyGetCorrespondenceOverview_WithValidRequest_ReturnsOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceOverview_InvalidPartyId_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            var failClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123abc"));

            // Act
            var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceOverview_CorrespondenceNotPublished_Returns404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_WithValidRequest_ReturnsOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsBadRequest()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            var failClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123abc"));

            // Act
            var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_WithCorrespondenceActions_IncludesStatuses()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert
            var content = await response.Content.ReadFromJsonAsync<List<LegacyGetCorrespondenceHistoryResponse>>(_serializerOptions);
            Assert.NotNull(content);
            Assert.Contains(content, status => status.User.PartyId == _digdirPartyId);
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Published.ToString()));
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Confirmed.ToString()));
            Assert.Contains(content, status => status.Status.Contains(CorrespondenceStatus.Archived.ToString()));
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_WithCorrespondenceActionsByDifferentParties_IncludesStatuses()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            int secondPartyId = 100;
            var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(100, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party
                    {
                        PartyId = secondPartyId,
                        OrgNumber = "",
                        SSN = "01018045678",
                        Resources = new List<string>(),
                        PartyTypeName = PartyType.Person,
                        UnitType = "Person",
                        Name = "Delegert test bruker",
                        PartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920")
                    });
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyUuid(new Guid("358C48B4-74A7-461F-A86F-48801DEEC920"), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Party
                    {
                        PartyId = secondPartyId,
                        OrgNumber = "",
                        SSN = "01018045678",
                        Resources = new List<string>(),
                        PartyTypeName = PartyType.Person,
                        UnitType = "Person",
                        Name = "Delegert test bruker",
                        PartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920")
                    });
                services.AddSingleton(mockRegisterService.Object);
            });
            

            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);            
            await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/markAsRead", null);
            var secondClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, secondPartyId.ToString()));
            await secondClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/markasread", null);
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);
            await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/archive", null);

            // Act
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert
            var content = await response.Content.ReadFromJsonAsync<List<LegacyGetCorrespondenceHistoryResponse>>(_serializerOptions);
            Assert.NotNull(content);
            Assert.Contains(content, status => status.User.PartyId == _digdirPartyId);
            Assert.Contains(content, status => (status.User.PartyId == _digdirPartyId && status.Status.Contains(CorrespondenceStatus.Published.ToString())));
            Assert.Contains(content, status => (status.User.PartyId == _digdirPartyId && status.Status.Contains(CorrespondenceStatus.Read.ToString())));
            Assert.Contains(content, status => (status.User.PartyId == _digdirPartyId && status.Status.Contains(CorrespondenceStatus.Confirmed.ToString())));
            Assert.Contains(content, status => (status.User.PartyId == _digdirPartyId && status.Status.Contains(CorrespondenceStatus.Archived.ToString())));
            Assert.Contains(content, status => status.User.PartyId == secondPartyId);
            Assert.Contains(content, status => (status.User.PartyId == secondPartyId && status.Status.Contains(CorrespondenceStatus.Read.ToString())));
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_Attachment_Contains_name()
        {

            // Arrange
            var attachmentId = await AttachmentHelper.GetPublishedAttachment(_senderClient, _serializerOptions);
            var payload = new CorrespondenceBuilder().CreateCorrespondence().WithExistingAttachments([attachmentId]).Build();

            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            var response = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview");
            var content = await response.Content.ReadFromJsonAsync<LegacyCorrespondenceOverviewExt>(_serializerOptions);
            Assert.NotNull(content);
            Assert.Equal("Test file", content.Attachments.First().Name);
        }

        [Fact]
        public async Task LegacyGetCorrespondenceHistory_InvalidPartyId_ReturnsUnauthorized()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            var factory = new UnitWebApplicationFactory((IServiceCollection services) =>
            {
                var mockRegisterService = new Mock<IAltinnRegisterService>();
                mockRegisterService
                    .Setup(service => service.LookUpPartyByPartyId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Party)null);
                services.AddSingleton(mockRegisterService.Object);
            });
            var failClient = factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.LegacyScope), (_partyIdClaim, "123"));

            // Act
            var response = await failClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/history");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
