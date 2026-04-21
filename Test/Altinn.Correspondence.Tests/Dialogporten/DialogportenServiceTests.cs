using System.Net;
using System.Text;
using System.Text.Json;
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
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
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
        var mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
        var options = Options.Create(new GeneralSettings { CorrespondenceBaseUrl = "https://correspondence.example" });

        var service = new DialogportenService(httpClient,
                                              mockRepo.Object,
                                              mockCorrespondenceForwardingEventRepository.Object,
                                              mockAltinnRegisterService.Object,
                                              options,
                                              mockLogger.Object,
                                              mockIdem.Object,
                                              mockResourceRegistryService.Object);
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
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
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
        var mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
        var options = Options.Create(new GeneralSettings { CorrespondenceBaseUrl = "https://correspondence.example" });

        // Setup forwarding event repository to track DialogActivityId assignments
        mockCorrespondenceForwardingEventRepository
            .Setup(r => r.SetDialogActivityId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DialogportenService(httpClient,
                                              mockRepo.Object,
                                              mockCorrespondenceForwardingEventRepository.Object,
                                              mockAltinnRegisterService.Object,
                                              options,
                                              mockLogger.Object,
                                              mockIdem.Object,
                                              mockResourceRegistryService.Object);
        return (service, mockCorrespondenceForwardingEventRepository, mockAltinnRegisterService, () => capturedRequestBody);
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
            .Setup(a => a.LookUpPartyByPartyUuid(delegatedUserPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = delegatedUserPartyUuid,
                SSN = "12345678901",
                PartyTypeName = PartyType.Person,
                Name = "Test Person"
            });

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        // Verify DialogActivityId was set
        forwardingRepoMock.Verify(r => r.SetDialogActivityId(forwardingEventId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify party lookup was called
        altinnRegisterMock.Verify(a => a.LookUpPartyByPartyUuid(delegatedUserPartyUuid, It.IsAny<CancellationToken>()), Times.Once);

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
        Assert.Contains("urn:altinn:person:identifier-no:12345678901", forwardingActivity.PerformedBy.ActorId);
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
            .Setup(a => a.LookUpPartyByPartyUuid(delegatedUserPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = delegatedUserPartyUuid,
                SSN = "12345678901",
                PartyTypeName = PartyType.Person,
                Name = "Test Person"
            });

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
            .Setup(a => a.LookUpPartyByPartyUuid(delegatedUserPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = delegatedUserPartyUuid,
                SSN = "12345678901",
                PartyTypeName = PartyType.Person,
                Name = "Sender Person"
            });

        altinnRegisterMock
            .Setup(a => a.LookUpPartyByPartyUuid(forwardedToUserUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = forwardedToUserUuid,
                SSN = "98765432109",
                PartyTypeName = PartyType.Person,
                Name = "Recipient Person"
            });

        // Act
        var resultId = await service.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence);

        // Assert
        Assert.Equal("dialog-id", resultId);

        forwardingRepoMock.Verify(r => r.SetDialogActivityId(forwardingEventId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify both party lookups were called
        altinnRegisterMock.Verify(a => a.LookUpPartyByPartyUuid(delegatedUserPartyUuid, It.IsAny<CancellationToken>()), Times.Once);
        altinnRegisterMock.Verify(a => a.LookUpPartyByPartyUuid(forwardedToUserUuid, It.IsAny<CancellationToken>()), Times.Once);

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
            .Setup(a => a.LookUpPartyByPartyUuid(delegatedUserPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = delegatedUserPartyUuid,
                SSN = "12345678901",
                PartyTypeName = PartyType.Person,
                Name = "Forwarder"
            });

        altinnRegisterMock
            .Setup(a => a.LookUpPartyByPartyUuid(forwardedToUserUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = forwardedToUserUuid,
                SSN = "98765432109",
                PartyTypeName = PartyType.Person,
                Name = "Delegate"
            });

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
            .OrderBy(a => a.CreatedAt)
            .ToList();

        Assert.Equal(3, forwardingActivities.Count);

        // Verify they are ordered by ForwardedOnDate
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
            .Setup(a => a.LookUpPartyByPartyUuid(forwardedByPartyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party
            {
                PartyUuid = forwardedByPartyUuid,
                SSN = "12345678901",
                PartyTypeName = PartyType.Person
            });

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
}
