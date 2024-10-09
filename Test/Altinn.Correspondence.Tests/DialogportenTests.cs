using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Validations;
using Moq;
using Org.BouncyCastle.Tls;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests;

public class DialogportenTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _responseSerializerOptions;

    public DialogportenTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }

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

    // TODO, test called InitializeCorrespondence_WithDialogToken_Fails
    // TODO, test called GetCorrespondenceContent_WithDialogTokenFromRecipient_Succeeds
    // TODO, test called GetCorrespondenceContent_WithDialogTokenFromSomeoneElse_Fails

    [Fact]
    public async Task InitializeCorrespondence_WithDialogToken_Fails()
    {
        // Arrange
        var dialogTokenClient = _factory.CreateClientWithDialogportenClaims(null);
        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();

        // Act
        var initializeCorrespondenceResponse = await dialogTokenClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, initializeCorrespondenceResponse.StatusCode);
    }

    [Fact]
    public async Task GetCorrespondenceContent_WithDialogTokenFromRecipient_Succeeds()
    {
        // Arrange
        var senderClient = _factory.CreateClientWithAddedClaims(("scope", AuthorizationConstants.SenderScope));
        var correspondenceToBeMade = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var initializeCorrespondenceResponse = await senderClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondenceToBeMade);
        var initializedCorrespondence = await initializeCorrespondenceResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(_responseSerializerOptions);
        using var scope = _factory.Services.CreateScope();
        var correspondence = await scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>().GetCorrespondenceById(initializedCorrespondence.CorrespondenceIds[0], false, false, CancellationToken.None);
        var config = _factory.Services.GetService<IConfiguration>();
        var dialogportenSettings = new DialogportenSettings();
        config.GetSection(nameof(DialogportenSettings)).Bind(dialogportenSettings);
        var dialogTokenClient = _factory.CreateClientWithDialogportenClaims(dialogportenSettings.Issuer, ("p", FormatHelper.GetRecipientUrn(correspondence)));

        // Act
        var contentResponse = await dialogTokenClient.GetAsync("correspondence/api/v1/correspondence/" + initializedCorrespondence.CorrespondenceIds[0] + "/content");

        // Assert
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        Assert.Equal("application/vnd.dialogporten.frontchannelembed+json; type=markdown; charset=utf-8", contentResponse.Content.Headers.ContentType?.ToString());
    }
}
