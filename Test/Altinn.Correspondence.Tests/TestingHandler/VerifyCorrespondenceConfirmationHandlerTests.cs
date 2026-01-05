using Altinn.Correspondence.Application.ConfirmCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class VerifyCorrespondenceConfirmationHandlerTests
{
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock = new();
    private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<IDialogportenService> _dialogportenServiceMock = new();
    private readonly Mock<ILogger<VerifyCorrespondenceConfirmationHandler>> _loggerMock = new();

    private readonly VerifyCorrespondenceConfirmationHandler _handler;

    public VerifyCorrespondenceConfirmationHandlerTests()
    {
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() => Guid.NewGuid().ToString());

        _correspondenceStatusRepositoryMock
            .Setup(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _handler = new VerifyCorrespondenceConfirmationHandler(
            _correspondenceRepositoryMock.Object,
            _correspondenceStatusRepositoryMock.Object,
            _backgroundJobClientMock.Object,
            _dialogportenServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task VerifyPatchAndCommitConfirmation_WhenDialogIsConfirmed_CommitsStatusAndEnqueuesSideEffects()
    {
        // Arrange
        var partyUuid = Guid.NewGuid();
        var partyId = 123;
        var operationTimestamp = DateTimeOffset.UtcNow;
        const string callerUrn = "urn:altinn:person:identifier-no:12018012345";

        var correspondence = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Fetched)
            .Build();

        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        _dialogportenServiceMock
            .Setup(x => x.VerifyCorrespondenceDialogPatchedToConfirmed(correspondence.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.VerifyPatchAndCommitConfirmation(correspondence.Id, partyUuid, partyId, operationTimestamp, callerUrn, CancellationToken.None);

        // Assert
        _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
            It.Is<CorrespondenceStatusEntity>(s =>
                s.CorrespondenceId == correspondence.Id &&
                s.Status == CorrespondenceStatus.Confirmed &&
                s.PartyUuid == partyUuid),
            It.IsAny<CancellationToken>()), Times.Once);

        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IEventBus)),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);

        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IDialogportenService) && job.Method.Name == "CreateConfirmedActivity"),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }

    [Fact]
    public async Task VerifyPatchAndCommitConfirmation_WhenDialogNotConfirmed_ThrowsAndDoesNotCommit()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Fetched)
            .Build();

        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        _dialogportenServiceMock
            .Setup(x => x.VerifyCorrespondenceDialogPatchedToConfirmed(correspondence.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act + Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.VerifyPatchAndCommitConfirmation(correspondence.Id, Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "caller", CancellationToken.None));

        _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyPatchAndCommitConfirmation_WhenAlreadyConfirmed_IsIdempotent()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithStatus(CorrespondenceStatus.Fetched)
            .WithStatus(CorrespondenceStatus.Confirmed)
            .Build();

        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        // Act
        await _handler.VerifyPatchAndCommitConfirmation(correspondence.Id, Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "caller", CancellationToken.None);

        // Assert
        _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundJobClientMock.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
        _dialogportenServiceMock.Verify(x => x.VerifyCorrespondenceDialogPatchedToConfirmed(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
