using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;
using System.Text.Json;

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
        var testFactory = new CustomWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(mockDialogportenService.Object);
        });

        var correspondence = InitializeCorrespondenceFactory.BasicCorrespondences();
        var testClient = testFactory.CreateDefaultClient();

        // Act
        var initializeCorrespondenceResponse = await testClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        mockDialogportenService.Verify(
            x => x.CreateCorrespondenceDialog(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(correspondence.Recipients.Count));
    }
}
