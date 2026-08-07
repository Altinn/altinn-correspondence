using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CheckForwardedCorrespondenceDelivery;
using Altinn.Correspondence.Application.ForwardCorrespondence;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Extensions;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class ForwardCorrespondenceHandlerTests
{
    private readonly Mock<IAltinnAuthorizationService> _altinnAuthorizationServiceMock = new();
    private readonly Mock<IAltinnNotificationService> _altinnNotificationServiceMock = new();
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock = new();
    private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock = new();
    private readonly Mock<ICorrespondenceForwardingEventRepository> _forwardingEventRepositoryMock = new();
    private readonly Mock<IStorageRepository> _storageRepositoryMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<ILogger<ForwardCorrespondenceHandler>> _loggerMock = new();

    private readonly ForwardCorrespondenceHandler _handler;

    private static readonly Guid PartyUuid = Guid.NewGuid();

    public ForwardCorrespondenceHandlerTests()
    {
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        _handler = new ForwardCorrespondenceHandler(
            _altinnAuthorizationServiceMock.Object,
            _altinnNotificationServiceMock.Object,
            _correspondenceRepositoryMock.Object,
            _altinnRegisterServiceMock.Object,
            _forwardingEventRepositoryMock.Object,
            new ComposedEmailHelper(_storageRepositoryMock.Object),
            _backgroundJobClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Process_UserIsNull_ReturnsNoAccessToResource()
    {
        var request = new ForwardCorrespondenceRequest { CorrespondenceId = Guid.NewGuid(), ForwardTo = "test@example.com" };

        var result = await _handler.Process(request, null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(AuthorizationErrors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_CorrespondenceNotFound_ReturnsCorrespondenceNotFound()
    {
        var correspondenceId = Guid.NewGuid();
        var request = new ForwardCorrespondenceRequest { CorrespondenceId = correspondenceId, ForwardTo = "test@example.com" };
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync((CorrespondenceEntity?)null);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.CorrespondenceNotFound, result.AsT1);
    }

    [Fact]
    public async Task Process_UserLacksRecipientAccess_ReturnsNoAccessToResource()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id);
        SetupCorrespondence(correspondence);
        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), correspondence, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(AuthorizationErrors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_ForwardingNotAllowedOnCorrespondence_ReturnsForwardingNotAllowed()
    {
        var correspondence = new CorrespondenceEntityBuilder()
            .WithAllowForwarding(false)
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Read)
            .Build();
        var request = BuildRequest(correspondence.Id);
        SetupCorrespondence(correspondence, hasAccess: true);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.ForwardingNotAllowed, result.AsT1);
    }

    [Fact]
    public async Task Process_ForwardingTextTooLong_ReturnsForwardingTextTooLong()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id, forwardingText: new string('a', 201));
        SetupCorrespondence(correspondence, hasAccess: true);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.ForwardingTextTooLong, result.AsT1);
    }

    [Fact]
    public async Task Process_ForwardingTextIsNotPlainText_ReturnsForwardingTextIsNotPlainText()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id, forwardingText: "<b>hei</b>");
        SetupCorrespondence(correspondence, hasAccess: true);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.ForwardingTextIsNotPlainText, result.AsT1);
    }

    [Fact]
    public async Task Process_CorrespondenceHasNotBeenRead_ReturnsForwardBeforeRead()
    {
        var correspondence = new CorrespondenceEntityBuilder()
            .WithAllowForwarding(true)
            .WithStatus(CorrespondenceStatus.Published)
            .Build();
        var request = BuildRequest(correspondence.Id);
        SetupCorrespondence(correspondence, hasAccess: true);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.ForwardBeforeRead, result.AsT1);
    }

    [Fact]
    public async Task Process_AlreadyForwardedToRecipient_ReturnsCorrespondenceAlreadyForwardedToRecipient()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id);
        SetupCorrespondence(correspondence, hasAccess: true);
        _forwardingEventRepositoryMock
            .Setup(x => x.HasCorrespondenceBeenForwardedToRecipient(correspondence.Id, request.ForwardTo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.CorrespondenceAlreadyForwardedToRecipient, result.AsT1);
        _forwardingEventRepositoryMock.Verify(x => x.AddForwardingEventForSync(It.IsAny<CorrespondenceForwardingEventEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_CouldNotFindPartyUuidForCaller_ReturnsCouldNotFindPartyUuid()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id);
        SetupCorrespondence(correspondence, hasAccess: true);
        _altinnRegisterServiceMock
            .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Altinn.Register.Contracts.Party?)null);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(AuthorizationErrors.CouldNotFindPartyUuid, result.AsT1);
    }

    [Fact]
    public async Task Process_AddForwardingEventLosesRace_ReturnsCorrespondenceAlreadyForwardedToRecipient()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id);
        SetupCorrespondence(correspondence, hasAccess: true);
        _forwardingEventRepositoryMock
            .Setup(x => x.AddForwardingEventForSync(It.IsAny<CorrespondenceForwardingEventEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.Empty);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.CorrespondenceAlreadyForwardedToRecipient, result.AsT1);
        _altinnNotificationServiceMock.Verify(x => x.CreateComposedEmail(It.IsAny<ComposedEmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_ComposedEmailCreationFails_DeletesForwardingEventAndReturnsForwardingNotAllowed()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id);
        var forwardingEventId = SetupCorrespondence(correspondence, hasAccess: true);
        _altinnNotificationServiceMock
            .Setup(x => x.CreateComposedEmail(It.IsAny<ComposedEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComposedEmailResponse)null!);

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.ForwardingNotAllowed, result.AsT1);
        _forwardingEventRepositoryMock.Verify(x => x.DeleteForwardingEvent(forwardingEventId, It.IsAny<CancellationToken>()), Times.Once);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(CheckForwardedCorrespondenceDeliveryHandler)),
            It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task Process_NotificationServiceThrows_DeletesForwardingEventAndRethrows()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id);
        var forwardingEventId = SetupCorrespondence(correspondence, hasAccess: true);
        _altinnNotificationServiceMock
            .Setup(x => x.CreateComposedEmail(It.IsAny<ComposedEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Process(request, CreateUser(), CancellationToken.None));

        _forwardingEventRepositoryMock.Verify(x => x.DeleteForwardingEvent(forwardingEventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_ValidRequest_PersistsForwardingEventAndSchedulesDeliveryCheck_ReturnsForwardingEventId()
    {
        var correspondence = BuildReadCorrespondence();
        var request = BuildRequest(correspondence.Id, forwardingText: "Please see attached");
        var forwardingEventId = SetupCorrespondence(correspondence, hasAccess: true);
        var shipmentId = Guid.NewGuid();
        _altinnNotificationServiceMock
            .Setup(x => x.CreateComposedEmail(It.IsAny<ComposedEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComposedEmailResponse
            {
                NotificationOrderId = Guid.NewGuid(),
                Notification = new ComposedEmailNotificationResponse { ShipmentId = shipmentId }
            });

        var result = await _handler.Process(request, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(forwardingEventId, result.AsT0);
        _forwardingEventRepositoryMock.Verify(x => x.SetNotificationShipmentId(forwardingEventId, shipmentId, It.IsAny<CancellationToken>()), Times.Once);
        _forwardingEventRepositoryMock.Verify(x => x.DeleteForwardingEvent(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(CheckForwardedCorrespondenceDeliveryHandler) &&
                job.Method.Name == nameof(CheckForwardedCorrespondenceDeliveryHandler.Process) &&
                (Guid)job.Args[0] == shipmentId &&
                (Guid)job.Args[1] == forwardingEventId),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }

    private static CorrespondenceEntity BuildReadCorrespondence()
    {
        return new CorrespondenceEntityBuilder()
            .WithAllowForwarding(true)
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Read)
            .Build();
    }

    private static ForwardCorrespondenceRequest BuildRequest(Guid correspondenceId, string forwardTo = "recipient@example.com", string? forwardingText = null)
    {
        return new ForwardCorrespondenceRequest
        {
            CorrespondenceId = correspondenceId,
            ForwardTo = forwardTo,
            ForwardingText = forwardingText
        };
    }

    private Guid SetupCorrespondence(CorrespondenceEntity correspondence, bool hasAccess = true)
    {
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, true, true, false, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(correspondence);
        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), correspondence, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAccess);
        _forwardingEventRepositoryMock
            .Setup(x => x.HasCorrespondenceBeenForwardedToRecipient(correspondence.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _altinnRegisterServiceMock
            .Setup(x => x.LookUpPartyById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterServiceMockExtensions.BuildOrganization(PartyUuid, "991825827", partyId: 123));

        var forwardingEventId = Guid.NewGuid();
        _forwardingEventRepositoryMock
            .Setup(x => x.AddForwardingEventForSync(It.IsAny<CorrespondenceForwardingEventEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(forwardingEventId);
        return forwardingEventId;
    }

    private static ClaimsPrincipal CreateUser()
    {
        var identity = new ClaimsIdentity([new Claim("c", $"{UrnConstants.PersonIdAttribute}:10108000398")], "TestAuthType");
        return new ClaimsPrincipal(identity);
    }
}
