using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Altinn.Correspondence.Tests.TestingController.Legacy.Base;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests.TestingController.Legacy
{
    [Collection(nameof(CustomWebApplicationTestsCollection))]
    public class LegacyDeletionTests : LegacyTestBase
    {
        public LegacyDeletionTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task Delete_Published_Correspondence_GivesOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Act
            var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
            var overviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}");
            Assert.Equal(HttpStatusCode.NotFound, overviewResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_Published_IsConfirmedRequired_Correspondence_GivesOk()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().WithDueDateTime(DateTime.UtcNow.AddDays(5)).WithConfirmationNeeded(true).Build();

            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Act
            var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
            var overviewResponse = await _senderClient.GetAsync($"correspondence/api/v1/correspondence/{correspondence.CorrespondenceId}");
            Assert.Equal(HttpStatusCode.NotFound, overviewResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_Correspondence_InvalidParyId_Gives()
        {
            // Arrange
            var payload = new CorrespondenceBuilder().CreateCorrespondence().Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);
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
            var deleteResponse = await failClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_NonExistent_Correspondence_Returns404()
        {
            // Arrange
            Guid randomCorrespondenceId = Guid.NewGuid();

            // Act
            var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{randomCorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_ReadyForPublish_Correspondence_Returns404()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithRequestedPublishTime(DateTimeOffset.UtcNow.AddDays(1))
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);

            // Act
            var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_Published_Correspondence_WithConfirmation_WhenConfirmationNeeded_Gives_OK()
        {
            // Arrange
            var payload = new CorrespondenceBuilder()
                .CreateCorrespondence()
                .WithDueDateTime(DateTimeOffset.UtcNow.AddDays(1))
                .WithConfirmationNeeded(true)
                .Build();
            var correspondence = await CorrespondenceHelper.GetInitializedCorrespondence(_senderClient, _serializerOptions, payload);
            await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(_senderClient, _serializerOptions, correspondence.CorrespondenceId, CorrespondenceStatusExt.Published);

            // Act
            var fetchResponse = await _legacyClient.GetAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/overview"); // Fetch in order to be able to Confirm
            Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
            var confirmResponse = await _legacyClient.PostAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/confirm", null);
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
            var deleteResponse = await _legacyClient.DeleteAsync($"correspondence/api/v1/legacy/correspondence/{correspondence.CorrespondenceId}/purge");

            // Assert
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        }
    }

}
