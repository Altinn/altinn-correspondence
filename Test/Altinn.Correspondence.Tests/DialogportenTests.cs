using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Tests;

public class DialogportenTests
{
    [Fact]
    public async Task InitializeCorrespondence_CreatesInDialogporten()
    {
        // Arrange
        var mockDialogportenService = new Mock<IDialogportenService>();
        mockDialogportenService
            .Setup(x => x.CreateCorrespondenceDialog(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("mocked-dialog-id");
        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(mockDialogportenService.Object);
        });

        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var testClient = testFactory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));

        // Act
        var initializeCorrespondenceResponse = await testClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        mockDialogportenService.Verify(
            x => x.CreateCorrespondenceDialog(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(correspondence.Recipients.Count));
    }
}
