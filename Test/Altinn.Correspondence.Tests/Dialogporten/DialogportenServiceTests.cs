using System.Net;
using System.Text;
using System.Text.Json;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Altinn.Correspondence.Tests.Extensions;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.Dialogporten;

public class DialogportenServiceTests
{
    private static (DialogportenService service, Func<string> getLastRequestBody) CreateServiceWithMockedDialogPost(CorrespondenceEntity correspondence)
    {
        var capturedRequestBody = string.Empty;

        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri != null &&
                    m.RequestUri.AbsolutePath.EndsWith("/dialogporten/api/v1/serviceowner/dialogs")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"dialog-id\"", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://dialogporten.example/")
        };

        var mockRepo = new Mock<ICorrespondenceRepository>();
        mockRepo
            .Setup(r => r.GetCorrespondenceById(correspondence.Id, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        var mockIdem = new Mock<IIdempotencyKeyRepository>();
        mockIdem
            .Setup(i => i.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity e, CancellationToken _) => e);
        mockIdem
            .Setup(i => i.CreateRangeAsync(It.IsAny<IEnumerable<IdempotencyKeyEntity>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<DialogportenService>>();
        var mockResourceRegistryService = new Mock<IResourceRegistryService>();
        var mockCorrespondenceForwardingEventRepository = new Mock<ICorrespondenceForwardingEventRepository>();
        var mockCorrespondenceNotificationRepository = new Mock<ICorrespondenceNotificationRepository>();
        var mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
        var mockPartyUrnHelper = new Mock<Core.Services.PartyUrnHelper>(mockAltinnRegisterService.Object, Mock.Of<ILogger<Core.Services.PartyUrnHelper>>());
        var options = Options.Create(new GeneralSettings { CorrespondenceBaseUrl = "https://correspondence.example" });

        var service = new DialogportenService(httpClient,
                                              mockRepo.Object,
                                              mockCorrespondenceForwardingEventRepository.Object,
                                              mockCorrespondenceNotificationRepository.Object,
                                              mockAltinnRegisterService.Object,
                                              options,
                                              mockLogger.Object,
                                              mockIdem.Object,
                                              mockResourceRegistryService.Object,
                                              mockPartyUrnHelper.Object);
        return (service, () => capturedRequestBody);
    }

    private static (DialogportenService service, Mock<ICorrespondenceForwardingEventRepository> forwardingEventRepoMock, Mock<IAltinnRegisterService> altinnRegisterMock, Func<string> getLastRequestBody) CreateServiceWithForwardingEventSupport(CorrespondenceEntity correspondence)
    {
        var capturedRequestBody = string.Empty;

        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri != null &&
                    m.RequestUri.AbsolutePath.EndsWith("/dialogporten/api/v1/serviceowner/dialogs")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"dialog-id\"", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://dialogporten.example/")
        };

        var mockRepo = new Mock<ICorrespondenceRepository>();
        mockRepo
            .Setup(r => r.GetCorrespondenceById(correspondence.Id, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        var mockIdem = new Mock<IIdempotencyKeyRepository>();
        mockIdem
            .Setup(i => i.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity e, CancellationToken _) => e);
        mockIdem
            .Setup(i => i.CreateRangeAsync(It.IsAny<IEnumerable<IdempotencyKeyEntity>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<DialogportenService>>();
        var mockResourceRegistryService = new Mock<IResourceRegistryService>();
        var mockCorrespondenceForwardingEventRepository = new Mock<ICorrespondenceForwardingEventRepository>();
        var mockCorrespondenceNotificationRepository = new Mock<ICorrespondenceNotificationRepository>();
        var mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
        var mockPartyUrnHelperLogger = new Mock<ILogger<Core.Services.PartyUrnHelper>>();
        var mockPartyUrnHelper = new Mock<Core.Services.PartyUrnHelper>(mockAltinnRegisterService.Object, mockPartyUrnHelperLogger.Object);
        var options = Options.Create(new GeneralSettings { CorrespondenceBaseUrl = "https://correspondence.example" });

        // Setup forwarding event repository to track DialogActivityId assignments
        mockCorrespondenceForwardingEventRepository
            .Setup(r => r.SetDialogActivityId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DialogportenService(httpClient,
                                              mockRepo.Object,
                                              mockCorrespondenceForwardingEventRepository.Object,
                                              mockCorrespondenceNotificationRepository.Object,
                                              mockAltinnRegisterService.Object,
                                              options,
                                              mockLogger.Object,
                                              mockIdem.Object,
                                              mockResourceRegistryService.Object,
                                              mockPartyUrnHelper.Object);
        return (service, mockCorrespondenceForwardingEventRepository, mockAltinnRegisterService, () => capturedRequestBody);
    }

    private static (DialogportenService service, Mock<ICorrespondenceRepository> repoMock) CreateServiceWithMockedDialogPurge(CorrespondenceEntity correspondence, string dialogId)
    {
        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri != null &&
                    m.RequestUri.AbsolutePath.EndsWith($"/dialogporten/api/v1/serviceowner/dialogs/{dialogId}/actions/purge")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://dialogporten.example/")
        };

        var mockRepo = new Mock<ICorrespondenceRepository>();
        mockRepo
            .Setup(r => r.GetCorrespondenceById(correspondence.Id, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        mockRepo
            .Setup(r => r.RemoveExternalReference(correspondence, ReferenceType.DialogportenDialogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
        var mockPartyUrnHelper = new Mock<PartyUrnHelper>(mockAltinnRegisterService.Object, Mock.Of<ILogger<PartyUrnHelper>>());
        var options = Options.Create(new GeneralSettings { CorrespondenceBaseUrl = "https://correspondence.example" });

        var service = new DialogportenService(
            httpClient,
            mockRepo.Object,
            Mock.Of<ICorrespondenceForwardingEventRepository>(),
            mockAltinnRegisterService.Object,
            options,
            Mock.Of<ILogger<DialogportenService>>(),
            Mock.Of<IIdempotencyKeyRepository>(),
            Mock.Of<IResourceRegistryService>(),
            mockPartyUrnHelper.Object);

        return (service, mockRepo);
    }

    [Fact]
    public async Task CreateCorrespondenceDialog_TruncatesSearchTags_ToMax63AndSucceeds()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var longValue = new string('A', 100);
        var correspondence = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Initialized)
            .WithPropertyList(new Dictionary<string, string>
            {
                { "longValue", longValue }
            })
            .WithId(correspondenceId)
            .Build();
        var (service, getBody) = CreateServiceWithMockedDialogPost(correspondence);

        // Act
        var resultId = await service.CreateCorrespondenceDialog(correspondenceId);

        // Assert
        Assert.Equal("dialog-id", resultId);
        var capturedRequestBody = getBody();
        Assert.False(string.IsNullOrWhiteSpace(capturedRequestBody));

        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.SearchTags);
        Assert.NotEmpty(deserialized.SearchTags);
        Assert.All(deserialized.SearchTags, t => Assert.True(t.Value.Length <= 63));

        var expectedTruncated = longValue.Substring(0, 63);
        Assert.Contains(deserialized.SearchTags, t => t.Value == expectedTruncated);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithEmailForwardingEvent_ShouldIncludeForwardingActivity()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
        var forwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E");
        var forwardingEventId = Guid.NewGuid();

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
            {
                new CorrespondenceForwardingEventEntity
                {
                    Id = forwardingEventId,
                    CorrespondenceId = correspondenceId,
                    ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                    ForwardedByPartyUuid = delegatedUserPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = forwardedByUserUuid,
                    ForwardedToEmailAddress = "user1@awesometestusers.com",
                    ForwardingText = "Keep this as a backup in my email."
                }
            })
            .Build();

        // Set navigation properties
        foreach (var fwdEvent in correspondence.ForwardingEvents)
        {
            fwdEvent.Correspondence = correspondence;
        }

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        // Setup party lookup
        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(delegatedUserPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(delegatedUserPartyUuid, "10108000398", displayName: "Test Person"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        // Verify DialogActivityId was set
        forwardingRepoMock.Verify(r => r.SetDialogActivityId(forwardingEventId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify party lookup was called
        altinnRegisterMock.Verify(a => a.LookUpPartyById(delegatedUserPartyUuid.ToString(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify the request body contains the forwarding activity
        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        // Should have status activities + forwarding activity
        var forwardingActivity = deserialized.Activities.FirstOrDefault(a => 
            a.Description?.Any(d => d.Value.Contains("user1@awesometestusers.com")) == true);

        Assert.NotNull(forwardingActivity);
        Assert.Equal("Information", forwardingActivity!.Type);
        Assert.Equal("PartyRepresentative", forwardingActivity.PerformedBy.ActorType);
        Assert.Contains("urn:altinn:person:identifier-no:10108000398", forwardingActivity.PerformedBy.ActorId);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithMailboxForwardingEvent_ShouldIncludeForwardingActivity()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
        var forwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E");
        var forwardingEventId = Guid.NewGuid();

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
            {
                new CorrespondenceForwardingEventEntity
                {
                    Id = forwardingEventId,
                    CorrespondenceId = correspondenceId,
                    ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0)),
                    ForwardedByPartyUuid = delegatedUserPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = forwardedByUserUuid,
                    MailboxSupplier = "urn:altinn:organization:identifier-no:984661185"
                }
            })
            .Build();

        foreach (var fwdEvent in correspondence.ForwardingEvents)
        {
            fwdEvent.Correspondence = correspondence;
        }

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(delegatedUserPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(delegatedUserPartyUuid, "10108000398", displayName: "Test Person"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        forwardingRepoMock.Verify(r => r.SetDialogActivityId(forwardingEventId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        var mailboxForwardingActivity = deserialized.Activities.FirstOrDefault(a =>
            a.Type == "Information" &&
            a.Description != null &&
            a.Description.Any(d => d.LanguageCode == "nb" && d.Value.Contains("Digipost")));

        Assert.NotNull(mailboxForwardingActivity);
        var nbDescription = mailboxForwardingActivity!.Description.FirstOrDefault(d => d.LanguageCode == "nb");
        Assert.NotNull(nbDescription);
        Assert.Equal("sendte \"Default title\" til Digipost", nbDescription!.Value);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithInstanceDelegationEvent_ShouldIncludeForwardingActivity()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
        var forwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E");
        var forwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17");
        var forwardingEventId = Guid.NewGuid();

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
            {
                new CorrespondenceForwardingEventEntity
                {
                    Id = forwardingEventId,
                    CorrespondenceId = correspondenceId,
                    ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0)),
                    ForwardedByPartyUuid = delegatedUserPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = forwardedByUserUuid,
                    ForwardedToUserId = 456,
                    ForwardedToUserUuid = forwardedToUserUuid,
                    ForwardingText = "User2, - look into this for me please. - User1.",
                    ForwardedToEmailAddress = "user2@awesometestusers.com"
                }
            })
            .Build();

        foreach (var fwdEvent in correspondence.ForwardingEvents)
        {
            fwdEvent.Correspondence = correspondence;
        }

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(delegatedUserPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(delegatedUserPartyUuid, "10108000398", displayName: "Sender Person"));

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(forwardedToUserUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(forwardedToUserUuid, "13076800124", displayName: "Recipient Person"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        forwardingRepoMock.Verify(r => r.SetDialogActivityId(forwardingEventId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify both party lookups were called
        altinnRegisterMock.Verify(a => a.LookUpPartyById(delegatedUserPartyUuid.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        altinnRegisterMock.Verify(a => a.LookUpPartyById(forwardedToUserUuid.ToString(), It.IsAny<CancellationToken>()), Times.Once);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        var forwardingActivity = deserialized.Activities.FirstOrDefault(a =>
            a.Description?.Any(d => d.Value.Contains("Recipient Person")) == true);

        Assert.NotNull(forwardingActivity);
        Assert.Equal("Information", forwardingActivity!.Type);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithMultipleForwardingEvents_ShouldIncludeAllActivitiesInOrder()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");
        var forwardedByUserUuid = new Guid("9ECDE07C-CF64-42B0-BEBD-035F195FB77E");
        var forwardedToUserUuid = new Guid("1D5FD16E-2905-414A-AC97-844929975F17");

        var emailDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0));
        var mailboxDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 5, 0));
        var delegationDate = new DateTimeOffset(new DateTime(2024, 1, 6, 12, 15, 0));

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
            {
                new CorrespondenceForwardingEventEntity
                {
                    Id = Guid.NewGuid(),
                    CorrespondenceId = correspondenceId,
                    ForwardedOnDate = emailDate,
                    ForwardedByPartyUuid = delegatedUserPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = forwardedByUserUuid,
                    ForwardedToEmailAddress = "user1@awesometestusers.com",
                    ForwardingText = "Keep this as a backup in my email."
                },
                new CorrespondenceForwardingEventEntity
                {
                    Id = Guid.NewGuid(),
                    CorrespondenceId = correspondenceId,
                    ForwardedOnDate = mailboxDate,
                    ForwardedByPartyUuid = delegatedUserPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = forwardedByUserUuid,
                    MailboxSupplier = "urn:altinn:organization:identifier-no:984661185"
                },
                new CorrespondenceForwardingEventEntity
                {
                    Id = Guid.NewGuid(),
                    CorrespondenceId = correspondenceId,
                    ForwardedOnDate = delegationDate,
                    ForwardedByPartyUuid = delegatedUserPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = forwardedByUserUuid,
                    ForwardedToUserId = 456,
                    ForwardedToUserUuid = forwardedToUserUuid,
                    ForwardingText = "User2, - look into this for me please. - User1.",
                    ForwardedToEmailAddress = "user2@awesometestusers.com"
                }
            })
            .Build();

        foreach (var fwdEvent in correspondence.ForwardingEvents)
        {
            fwdEvent.Correspondence = correspondence;
        }

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(delegatedUserPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(delegatedUserPartyUuid, "10108000398", displayName: "Forwarder"));

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(forwardedToUserUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(forwardedToUserUuid, "13076800124", displayName: "Delegate"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        // Verify DialogActivityId was set for all 3 events
        forwardingRepoMock.Verify(r => r.SetDialogActivityId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        // Find forwarding activities (exclude status activities)
        var forwardingActivities = deserialized.Activities
            .Where(a => a.Description?.Any(d => 
                d.Value.Contains("user1@awesometestusers.com") || 
                d.Value.Contains("Digipost") || 
                d.Value.Contains("Delegate")) == true)
            .ToList();

        Assert.Equal(3, forwardingActivities.Count);

        // Verify they are in the correct emission order
        Assert.Equal(emailDate, forwardingActivities[0].CreatedAt);
        Assert.Equal(mailboxDate, forwardingActivities[1].CreatedAt);
        Assert.Equal(delegationDate, forwardingActivities[2].CreatedAt);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithNoForwardingEvents_ShouldNotIncludeForwardingActivities()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .Build();

        var (service, _, _, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        // Should only have status activities, no forwarding activities
        var forwardingActivities = deserialized.Activities
            .Where(a => a.Type == "Information" && 
                       a.Description?.Any(d => d.Value.Contains("@") || d.Value.Contains("Digipost") || d.Value.Contains("e-Boks")) == true)
            .ToList();

        Assert.Empty(forwardingActivities);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithExistingDialogActivityId_ShouldNotReassignId()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var forwardedByPartyUuid = Guid.NewGuid();
        var existingDialogActivityId = Guid.NewGuid();
        var forwardingEventId = Guid.NewGuid();

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .WithForwardingEvents(new List<CorrespondenceForwardingEventEntity>
            {
                new CorrespondenceForwardingEventEntity
                {
                    Id = forwardingEventId,
                    CorrespondenceId = correspondenceId,
                    DialogActivityId = existingDialogActivityId, // Already has an ID
                    ForwardedOnDate = new DateTimeOffset(new DateTime(2024, 1, 6, 11, 0, 0)),
                    ForwardedByPartyUuid = forwardedByPartyUuid,
                    ForwardedByUserId = 123,
                    ForwardedByUserUuid = Guid.NewGuid(),
                    ForwardedToEmailAddress = "test@example.com"
                }
            })
            .Build();

        foreach (var fwdEvent in correspondence.ForwardingEvents)
        {
            fwdEvent.Correspondence = correspondence;
        }

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(forwardedByPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(forwardedByPartyUuid, "10108000398"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        // Should NOT call SetDialogActivityId since it already exists
        forwardingRepoMock.Verify(r => r.SetDialogActivityId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify the existing DialogActivityId is used
        var forwardingActivity = deserialized!.Activities.FirstOrDefault(a =>
            a.Description?.Any(d => d.Value.Contains("test@example.com")) == true);

        Assert.NotNull(forwardingActivity);
        Assert.Equal(existingDialogActivityId.ToString(), forwardingActivity!.Id);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithReadStatus_ShouldUsePersonActorIdNotOrganization()
    {
        // Arrange - Reproduces Issue #1951
        var correspondenceId = Guid.NewGuid();
        var personPartyUuid = new Guid("b96ca314-021d-49b8-a267-376d55acd01f");
        var readTimestamp = new DateTimeOffset(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithRecipient($"{UrnConstants.OrganizationNumberAttribute}:910000001") // Fabricated test organization
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .Build();

        // Add Read status with correct PartyUuid
        correspondence.Statuses.Add(new CorrespondenceStatusEntity
        {
            Status = CorrespondenceStatus.Read,
            StatusChanged = readTimestamp,
            PartyUuid = personPartyUuid
        });

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        // Setup party lookup to return the person who actually performed the read action
        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(personPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(personPartyUuid, "10108000398", displayName: "Test Person"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        // Find the Read activity
        var readActivity = deserialized.Activities.FirstOrDefault(a => a.Type == "CorrespondenceOpened");

        Assert.NotNull(readActivity);
        Assert.Equal("PartyRepresentative", readActivity!.PerformedBy.ActorType);

        // ActorId should be the person URN, NOT the organization URN
        Assert.Contains("urn:altinn:person:identifier-no:10108000398", readActivity.PerformedBy.ActorId);
        Assert.DoesNotContain("urn:altinn:organizationnumber:910000001", readActivity.PerformedBy.ActorId);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithConfirmedStatus_ShouldUsePersonActorIdNotOrganization()
    {
        // Arrange - Reproduces Issue #1951
        var correspondenceId = Guid.NewGuid();
        var personPartyUuid = new Guid("b96ca314-021d-49b8-a267-376d55acd01f");
        var readTimestamp = new DateTimeOffset(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        var confirmTimestamp = new DateTimeOffset(new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc));

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithRecipient($"{UrnConstants.OrganizationNumberAttribute}:910000001") // Fabricated test organization
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .Build();

        correspondence.IsConfirmationNeeded = true;

        // Add Read and Confirmed statuses with correct PartyUuids
        correspondence.Statuses.Add(new CorrespondenceStatusEntity
        {
            Status = CorrespondenceStatus.Read,
            StatusChanged = readTimestamp,
            PartyUuid = personPartyUuid
        });
        correspondence.Statuses.Add(new CorrespondenceStatusEntity
        {
            Status = CorrespondenceStatus.Confirmed,
            StatusChanged = confirmTimestamp,
            PartyUuid = personPartyUuid
        });

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        // Setup party lookup
        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(personPartyUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(personPartyUuid, "10108000398", displayName: "Test Person"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        // Find the Confirmed activity
        var confirmedActivity = deserialized.Activities.FirstOrDefault(a => a.Type == "CorrespondenceConfirmed");

        Assert.NotNull(confirmedActivity);
        Assert.Equal("PartyRepresentative", confirmedActivity!.PerformedBy.ActorType);

        // ActorId should be the person URN, NOT the organization URN
        Assert.Contains("urn:altinn:person:identifier-no:10108000398", confirmedActivity.PerformedBy.ActorId);
        Assert.DoesNotContain("urn:altinn:organizationnumber:910000001", confirmedActivity.PerformedBy.ActorId);
    }

    [Fact]
    public async Task CreateCorrespondenceDialogForMigratedCorrespondence_WithBothReadAndConfirmedStatus_ShouldUseCorrectActorForEach()
    {
        // Arrange - Test case where different persons read and confirmed
        var correspondenceId = Guid.NewGuid();
        var personWhoReadUuid = new Guid("b96ca314-021d-49b8-a267-376d55acd01f");
        var personWhoConfirmedUuid = new Guid("c86ea314-021d-49b8-a267-376d55acd02a");
        var readTimestamp = new DateTimeOffset(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        var confirmTimestamp = new DateTimeOffset(new DateTime(2024, 1, 15, 11, 00, 0, DateTimeKind.Utc));

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithRecipient($"{UrnConstants.OrganizationNumberAttribute}:910000001") // Fabricated test organization
            .WithStatus(CorrespondenceStatus.Published, DateTimeOffset.UtcNow)
            .Build();

        correspondence.IsConfirmationNeeded = true;

        // Add Read and Confirmed statuses with different PartyUuids
        correspondence.Statuses.Add(new CorrespondenceStatusEntity
        {
            Status = CorrespondenceStatus.Read,
            StatusChanged = readTimestamp,
            PartyUuid = personWhoReadUuid
        });
        correspondence.Statuses.Add(new CorrespondenceStatusEntity
        {
            Status = CorrespondenceStatus.Confirmed,
            StatusChanged = confirmTimestamp,
            PartyUuid = personWhoConfirmedUuid
        });

        var (service, forwardingRepoMock, altinnRegisterMock, getBody) = CreateServiceWithForwardingEventSupport(correspondence);

        // Setup party lookups for both persons
        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(personWhoReadUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(personWhoReadUuid, "10108000398", displayName: "Person One"));

        altinnRegisterMock
            .Setup(a => a.LookUpPartyById(personWhoConfirmedUuid.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildPerson(personWhoConfirmedUuid, "13076800124", displayName: "Person Two"));

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        var capturedRequestBody = getBody();
        var deserialized = JsonSerializer.Deserialize<CreateDialogRequest>(capturedRequestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.Activities);

        var readActivity = deserialized.Activities.FirstOrDefault(a => a.Type == "CorrespondenceOpened");
        var confirmedActivity = deserialized.Activities.FirstOrDefault(a => a.Type == "CorrespondenceConfirmed");

        Assert.NotNull(readActivity);
        Assert.NotNull(confirmedActivity);

        // Verify read activity uses the person who read
        Assert.Contains("urn:altinn:person:identifier-no:10108000398", readActivity!.PerformedBy.ActorId);

        // Verify confirmed activity uses the person who confirmed
        Assert.Contains("urn:altinn:person:identifier-no:13076800124", confirmedActivity!.PerformedBy.ActorId);
    }

    [Fact]
    public async Task PurgeCorrespondenceDialog_WhenCorrespondenceHasDialogReference_RemovesExternalReference()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        const string dialogId = "dialog-123";

        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithDialogId(dialogId)
            .Build();

        var (service, mockRepo) = CreateServiceWithMockedDialogPurge(correspondence, dialogId);

        // Act
        await service.PurgeCorrespondenceDialog(correspondenceId);

        // Assert
        mockRepo.Verify(
            r => r.RemoveExternalReference(correspondence, ReferenceType.DialogportenDialogId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
