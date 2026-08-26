using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.SendNotificationOrder;
using Altinn.Correspondence.Application.UnreadConfidentialCorrespondence;
using Altinn.Correspondence.Common.Helpers.Models;
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
using Altinn.Correspondence.Application.Helpers;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class UnreadConfidentialCorrespondenceReminderHandlerTests
{
    private readonly Mock<ILogger<UnreadConfidentialCorrespondenceHandler>> _loggerMock;
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
    private readonly Mock<IConfidentialReminderRepository> _confidentialReminderRepositoryMock;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock;
    private readonly Mock<IDialogportenService> _dialogportenServiceMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly UnreadConfidentialCorrespondenceHandler _handler;

    public UnreadConfidentialCorrespondenceReminderHandlerTests()
    {
        _loggerMock = new Mock<ILogger<UnreadConfidentialCorrespondenceHandler>>();
        _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
        _confidentialReminderRepositoryMock = new Mock<IConfidentialReminderRepository>();
        _idempotencyKeyRepositoryMock = new Mock<IIdempotencyKeyRepository>();
        _dialogportenServiceMock = new Mock<IDialogportenService>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() => Guid.NewGuid().ToString());

        var dialogSynchronizerMock = new Mock<IConfidentialReminderDialogSynchronizer>();
        dialogSynchronizerMock
            .Setup(x => x.ExecuteForRecipientAsync(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<Guid>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<Guid>> op, CancellationToken ct) => op(ct));

        _handler = new UnreadConfidentialCorrespondenceHandler(
            _loggerMock.Object,
            _correspondenceRepositoryMock.Object,
            _confidentialReminderRepositoryMock.Object,
            _idempotencyKeyRepositoryMock.Object,
            _dialogportenServiceMock.Object,
            _backgroundJobClientMock.Object,
            dialogSynchronizerMock.Object);
    }

    private CorrespondenceEntity CreateUnreadCorrespondence(Guid correspondenceId)
    {
        return new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithRecipient("urn:altinn:organization:identifier-no:991825827")
            .WithStatus(CorrespondenceStatus.Published)
            .Build();
    }

    private void SetupCreateIdempotencyKeyReturnsInput()
    {
        _idempotencyKeyRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity key, CancellationToken _) => key);
    }

    [Fact]
    public async Task Process_CorrespondenceNotFound_ReturnsEarlyWithoutEnqueuingJobs()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorrespondenceEntity?)null);

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClientMock.Verify(
            x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(It.IsAny<ConfidentialReminderEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_CorrespondenceAlreadyRead_ReturnsEarlyWithoutEnqueuingJobs()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(CorrespondenceStatus.Published)
            .WithStatus(CorrespondenceStatus.Read)
            .Build();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClientMock.Verify(
            x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(It.IsAny<ConfidentialReminderEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(CorrespondenceStatus.Initialized)]
    [InlineData(CorrespondenceStatus.ReadyForPublish)]
    [InlineData(CorrespondenceStatus.Failed)]
    public async Task Process_CorrespondenceNotAvailableForRecipient_ReturnsEarlyWithoutCreatingReminder(CorrespondenceStatus status)
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var correspondence = new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithStatus(status)
            .Build();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClientMock.Verify(
            x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(It.IsAny<ConfidentialReminderEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _dialogportenServiceMock.Verify(
            x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_NoExistingRemindersForRecipient_CreatesIdempotencyKeyDialogAndSavesReminder()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        SetupCreateIdempotencyKeyReturnsInput();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync((ConfidentialReminderDialogDto r) => r.DialogId.ToString());

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(
                It.Is<IdempotencyKeyEntity>(k =>
                    k.PartyUrn == correspondence.Recipient &&
                    k.IdempotencyType == IdempotencyType.ConfidentialReminderDialog),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dialogportenServiceMock.Verify(
            x => x.CreateConfidentialReminderDialog(It.Is<ConfidentialReminderDialogDto>(r => r.DialogId != Guid.Empty)),
            Times.Once);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(
                It.Is<ConfidentialReminderEntity>(r =>
                    r.CorrespondenceId == correspondenceId &&
                    r.DialogId != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_DialogCreationThrows_StillPersistsReminderWithPreAllocatedDialogId()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        SetupCreateIdempotencyKeyReturnsInput();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ThrowsAsync(new Exception("Response from Dialogporten was not successful"));

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert — keep pre-allocated dialog id so concurrent creates remain idempotent
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(
                It.Is<ConfidentialReminderEntity>(r =>
                    r.CorrespondenceId == correspondenceId &&
                    r.DialogId != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_ExistingIdempotencyKey_ReusesKeyAsDialogIdWithoutCreatingNewKey()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var existingDialogId = Guid.CreateVersion7();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);
        _idempotencyKeyRepositoryMock
            .Setup(x => x.GetByPartyUrnAndTypeAsync(
                correspondence.Recipient,
                IdempotencyType.ConfidentialReminderDialog,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyKeyEntity
            {
                Id = existingDialogId,
                PartyUrn = correspondence.Recipient,
                IdempotencyType = IdempotencyType.ConfidentialReminderDialog
            });
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync(existingDialogId.ToString());

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _dialogportenServiceMock.Verify(
            x => x.CreateConfidentialReminderDialog(
                It.Is<ConfidentialReminderDialogDto>(r => r.DialogId == existingDialogId)),
            Times.Once);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(
                It.Is<ConfidentialReminderEntity>(r => r.DialogId == existingDialogId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_UnreadCorrespondence_AlwaysEnqueuesNotificationJobAndContinuation()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        SetupCreateIdempotencyKeyReturnsInput();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync((ConfidentialReminderDialogDto r) => r.DialogId.ToString());

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(j => j.Type == typeof(CreateNotificationOrderHandler)),
                It.IsAny<IState>()),
            Times.Once);
        _backgroundJobClientMock.Verify(
            x => x.Create(
                It.Is<Job>(j => j.Type == typeof(SendNotificationOrderHandler)),
                It.IsAny<IState>()),
            Times.Once);
    }
}
