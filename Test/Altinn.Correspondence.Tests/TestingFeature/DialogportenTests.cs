using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Application.LegacyUpdateCorrespondenceStatus;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingFeature;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class DialogportenTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _responseSerializerOptions;
    public readonly int _digdirPartyId = 50952483;

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
        hangfireBackgroundJobClient.Setup(x => x.Create(
            It.IsAny<Job>(),
            It.IsAny<IState>()))
            .Returns("123456");
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
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
        Assert.Contains(hangfireBackgroundJobClient.Invocations, invocation => invocation.Arguments[0].ToString() == "InitializeCorrespondencesHandler.CreateDialogportenDialog");
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
        await CorrespondenceHelper.WaitForCorrespondenceStatusUpdate(senderClient, _responseSerializerOptions, initializedCorrespondence!.Correspondences[0].CorrespondenceId, CorrespondenceStatusExt.Published);
        using var scope = _factory.Services.CreateScope();
        var correspondence = await scope.ServiceProvider.GetRequiredService<ICorrespondenceRepository>().GetCorrespondenceById(initializedCorrespondence.Correspondences[0].CorrespondenceId, false, false, false, CancellationToken.None);
        var config = _factory.Services.GetService<IConfiguration>();
        var dialogportenSettings = new DialogportenSettings();
        config!.GetSection(nameof(DialogportenSettings)).Bind(dialogportenSettings);
        var dialogTokenClient = _factory.CreateClientWithDialogportenClaims(dialogportenSettings.Issuer, ("p", DialogportenCorrespondenceMapper.GetRecipientUrn(correspondence!)),
            ("ID", correspondence!.Recipient));

        // Act
        var contentResponse = await dialogTokenClient.GetAsync("correspondence/api/v1/correspondence/" + initializedCorrespondence.Correspondences[0].CorrespondenceId + "/content");

        // Assert
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", contentResponse.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task FailedPublish_DialogIsPurged()
    {
        // Arrange
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var contactReservationRegistry = new Mock<IContactReservationRegistryService>();
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.IsPersonReserved(It.IsAny<string>())).ReturnsAsync(true);
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.GetReservedRecipients(It.IsAny<List<string>>())).ReturnsAsync(new List<string>());
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
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
                    Status = CorrespondenceStatus.Initialized,
                    StatusChanged = DateTimeOffset.UtcNow
                }
            },
            ExternalReferences = new List<Core.Models.Entities.ExternalReferenceEntity>()
            {
                new Core.Models.Entities.ExternalReferenceEntity()
                {
                    ReferenceType = ReferenceType.DialogportenDialogId,
                    ReferenceValue = "dialogId"
                }
            }
        }, CancellationToken.None);
        var correspondenceId = initializedCorrespondence.Id;
        Assert.NotNull(correspondenceId);
        var handler = scope.ServiceProvider.GetRequiredService<PublishCorrespondenceHandler>();
        await handler.Process(correspondenceId, null, CancellationToken.None);

        // Assert
        Assert.Contains(hangfireBackgroundJobClient.Invocations, invocation => invocation.Arguments[0].ToString() == "IDialogportenService.PurgeCorrespondenceDialog");
    }

    [Fact]
    public async Task PublishCorrespondence_WithMissingDialogPortenExternalReference_Fails()
    {
        // Arrange
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var contactReservationRegistry = new Mock<IContactReservationRegistryService>();
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.IsPersonReserved(It.IsAny<string>())).ReturnsAsync(false);
        contactReservationRegistry.Setup(contactReservationRegistry => contactReservationRegistry.GetReservedRecipients(It.IsAny<List<string>>())).ReturnsAsync(new List<string>());
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
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
                    Status = CorrespondenceStatus.ReadyForPublish,
                    StatusChanged = DateTimeOffset.UtcNow
                }
            },
        }, CancellationToken.None);
        var correspondenceId = initializedCorrespondence.Id;
        var handler = scope.ServiceProvider.GetRequiredService<PublishCorrespondenceHandler>();
        var result = await handler.Process(correspondenceId, null, CancellationToken.None);

        var processedCorrespondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, CancellationToken.None);

        // Assert
        Assert.NotNull(processedCorrespondence);
        Assert.Contains(processedCorrespondence.Statuses, s => s.Status == CorrespondenceStatus.Failed);
        var failedStatus = processedCorrespondence.Statuses.Find(s => s.Status == CorrespondenceStatus.Failed);
        Assert.Equal($"Dialogporten dialog not created for correspondence {correspondenceId}", failedStatus?.StatusText);
        Assert.DoesNotContain(hangfireBackgroundJobClient.Invocations, invocation => invocation.Arguments[0].ToString() == "IDialogportenService.PurgeCorrespondenceDialog");
    }

    [Fact]
    public async Task GetCorrespondenceContent_CreatesDialogPortenOpenedActivity()
    {
        // Arrange
        var correspondenceEntityBuilder = new CorrespondenceEntityBuilder();
        var testCorrespondence = correspondenceEntityBuilder
            .WithStatus(CorrespondenceStatus.ReadyForPublish)
            .WithStatus(CorrespondenceStatus.Published)
            .WithExternalReference(ReferenceType.DialogportenDialogId, "dialogId")
            .Build();
        var correspondenceRepository = new Mock<ICorrespondenceRepository>();
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var altinnRegisterService = new Mock<IAltinnRegisterService>();
        var correspondenceStatusRepository = new Mock<ICorrespondenceStatusRepository>();
        correspondenceStatusRepository.Setup(correspondenceStatusRepository => correspondenceStatusRepository
            .AddCorrespondenceStatus(It.IsAny<Core.Models.Entities.CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Guid());
        correspondenceRepository.Setup(correspondenceRepository => correspondenceRepository
            .GetCorrespondenceById(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(testCorrespondence);
        altinnRegisterService.Setup(altinnRegisterService => altinnRegisterService.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Core.Models.Entities.Party
        {
            PartyUuid = Guid.NewGuid()
        });
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) =>
        {
            services.AddSingleton(correspondenceRepository.Object);
            services.AddSingleton(correspondenceStatusRepository.Object);
            services.AddSingleton(hangfireBackgroundJobClient.Object);
            services.AddSingleton(altinnRegisterService.Object);
        });

        var correspondence = new CorrespondenceBuilder().CreateCorrespondence().Build();
        var testClient = testFactory.CreateSenderClient();

        // Act
        using var scope = testFactory.Services.CreateScope();
        GetCorrespondenceOverviewRequest request = new GetCorrespondenceOverviewRequest()
        {
            CorrespondenceId = testCorrespondence.Id,
            OnlyGettingContent = true
        };
        var handler = scope.ServiceProvider.GetRequiredService<GetCorrespondenceOverviewHandler>();
        var result = await handler.Process(request, new(), CancellationToken.None);

        Assert.IsType<GetCorrespondenceOverviewResponse>(result.Value);

        // Assert
        Assert.Single(hangfireBackgroundJobClient.Invocations);
        Assert.Contains(hangfireBackgroundJobClient.Invocations, invocation => invocation.Arguments[0].ToString() == "IDialogportenService.CreateOpenedActivity");
    }
}