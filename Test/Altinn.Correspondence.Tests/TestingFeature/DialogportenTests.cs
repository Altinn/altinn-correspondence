using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingFeature;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class DialogportenTests
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
            .Setup(x => x.CreateCorrespondenceDialog(It.IsAny<Guid>()))
            .ReturnsAsync("mocked-dialog-id");
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(mockDialogportenService.Object);
            services.AddSingleton(hangfireBackgroundJobClient.Object);
        });

        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var testClient = testFactory.CreateSenderClient();

        // Act
        var initializeCorrespondenceResponse = await testClient.PostAsJsonAsync("correspondence/api/v1/correspondence", correspondence);

        // Assert
        initializeCorrespondenceResponse.EnsureSuccessStatusCode();
        Assert.True(hangfireBackgroundJobClient.Invocations.Any(invocation => invocation.Arguments[0].ToString() == "InitializeCorrespondencesHandler.CreateDialogportenDialog"));
    }

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
        var correspondence = await scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>().GetCorrespondenceById(initializedCorrespondence.Correspondences[0].CorrespondenceId, false, false, CancellationToken.None);
        var config = _factory.Services.GetService<IConfiguration>();
        var dialogportenSettings = new DialogportenSettings();
        config.GetSection(nameof(DialogportenSettings)).Bind(dialogportenSettings);
        var dialogTokenClient = _factory.CreateClientWithDialogportenClaims(dialogportenSettings.Issuer, ("p", DialogportenCorrespondenceMapper.GetRecipientUrn(correspondence)),
            ("ID", correspondence.Recipient));

        // Act
        var contentResponse = await dialogTokenClient.GetAsync("correspondence/api/v1/correspondence/" + initializedCorrespondence.Correspondences[0].CorrespondenceId + "/content");

        // Assert
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", contentResponse.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task FailedPublish_DialogIsPurged() {
        // Arrange
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var contactReservationRegistry = new Mock<IContactReservationRegistryService>();
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.IsPersonReserved(It.IsAny<string>())).ReturnsAsync(true);
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.GetReservedRecipients(It.IsAny<List<string>>())).ReturnsAsync(new List<string>());
        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(hangfireBackgroundJobClient.Object);
            services.AddSingleton(contactReservationRegistry.Object);
        });

        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var testClient = testFactory.CreateSenderClient();

        // Act
        using var scope = testFactory.Services.CreateScope();
        var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
        var initializedCorrespondence = await correspondenceRepository.CreateCorrespondence(new Core.Models.Entities.CorrespondenceEntity()
        {
            Created = DateTimeOffset.UtcNow,
            Recipient = correspondence.Recipients[0],
            RequestedPublishTime = DateTimeOffset.UtcNow,
            ResourceId = correspondence.Correspondence.ResourceId,
            Sender = correspondence.Correspondence.Sender,
            SendersReference = correspondence.Correspondence.SendersReference,
            Statuses = new List<Core.Models.Entities.CorrespondenceStatusEntity>()
            {
                new Core.Models.Entities.CorrespondenceStatusEntity()
                {
                    Status = Core.Models.Enums.CorrespondenceStatus.Initialized,
                    StatusChanged = DateTimeOffset.UtcNow
                }
            },
            ExternalReferences = new List<Core.Models.Entities.ExternalReferenceEntity>()
            {
                new Core.Models.Entities.ExternalReferenceEntity()
                {
                    ReferenceType = Core.Models.Enums.ReferenceType.DialogportenDialogId,
                    ReferenceValue = "dialogId"
                }
            }
        }, CancellationToken.None);
        var correspondenceId = initializedCorrespondence.Id;
        Assert.NotNull(correspondenceId);
        var handler = scope.ServiceProvider.GetRequiredService<PublishCorrespondenceHandler>();
        await handler.Process(correspondenceId, null, CancellationToken.None);

        // Assert
        Assert.True(hangfireBackgroundJobClient.Invocations.Any(invocation => invocation.Arguments[0].ToString() == "IDialogportenService.PurgeCorrespondenceDialog"));
    }

    [Fact]
    public async Task PublishCorrespondence_WithMissingDialogPortenExternalReference_Fails()
    {
        // Arrange
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var contactReservationRegistry = new Mock<IContactReservationRegistryService>();
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.IsPersonReserved(It.IsAny<string>())).ReturnsAsync(false);
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.GetReservedRecipients(It.IsAny<List<string>>())).ReturnsAsync(new List<string>());
        var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(hangfireBackgroundJobClient.Object);
            services.AddSingleton(contactReservationRegistry.Object);
        });

        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var testClient = testFactory.CreateSenderClient();

        // Act
        using var scope = testFactory.Services.CreateScope();
        var correspondenceRepository = scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>();
        var initializedCorrespondence = await correspondenceRepository.CreateCorrespondence(new Core.Models.Entities.CorrespondenceEntity()
        {
            Created = DateTimeOffset.UtcNow,
            Recipient = correspondence.Recipients[0],
            RequestedPublishTime = DateTimeOffset.UtcNow.AddSeconds(-5),
            ResourceId = correspondence.Correspondence.ResourceId,
            Sender = correspondence.Correspondence.Sender,
            SendersReference = correspondence.Correspondence.SendersReference,
            Statuses = new List<Core.Models.Entities.CorrespondenceStatusEntity>()
            {
                new Core.Models.Entities.CorrespondenceStatusEntity()
                {
                    Status = Core.Models.Enums.CorrespondenceStatus.ReadyForPublish,
                    StatusChanged = DateTimeOffset.UtcNow
                }
            },
        }, CancellationToken.None);
        var correspondenceId = initializedCorrespondence.Id;
        var handler = scope.ServiceProvider.GetRequiredService<PublishCorrespondenceHandler>();
        var result = await handler.Process(correspondenceId, null, CancellationToken.None);

        var processedCorrespondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, CancellationToken.None);

        // Assert
        Assert.NotNull(processedCorrespondence);
        Assert.Contains(processedCorrespondence.Statuses, s => s.Status == Core.Models.Enums.CorrespondenceStatus.Failed);
        var failedStatus = processedCorrespondence.Statuses.Find(s => s.Status == Core.Models.Enums.CorrespondenceStatus.Failed);
        Assert.Equal($"Dialogporten dialog not created for correspondence {correspondenceId}", failedStatus?.StatusText);
        Assert.DoesNotContain(hangfireBackgroundJobClient.Invocations, invocation => invocation.Arguments[0].ToString() == "IDialogportenService.PurgeCorrespondenceDialog");
    }
}
