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

namespace Altinn.Correspondence.Tests.TestingHandler;

public class UnreadConfidentialCorrespondenceReminderHandlerTests
{
    private readonly Mock<ILogger<UnreadConfidentialCorrespondenceHandler>> _loggerMock;
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
    private readonly Mock<IConfidentialReminderRepository> _confidentialReminderRepositoryMock;
    private readonly Mock<IDialogportenService> _dialogportenServiceMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly UnreadConfidentialCorrespondenceHandler _handler;

    public UnreadConfidentialCorrespondenceReminderHandlerTests()
    {
        _loggerMock = new Mock<ILogger<UnreadConfidentialCorrespondenceHandler>>();
        _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
        _confidentialReminderRepositoryMock = new Mock<IConfidentialReminderRepository>();
        _dialogportenServiceMock = new Mock<IDialogportenService>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() => Guid.NewGuid().ToString());

        _handler = new UnreadConfidentialCorrespondenceHandler(
            _loggerMock.Object,
            _correspondenceRepositoryMock.Object,
            _confidentialReminderRepositoryMock.Object,
            _dialogportenServiceMock.Object,
            _backgroundJobClientMock.Object);
    }

    private CorrespondenceEntity CreateUnreadCorrespondence(Guid correspondenceId)
    {
        return new CorrespondenceEntityBuilder()
            .WithId(correspondenceId)
            .WithRecipient("urn:altinn:organization:identifier-no:991825827")
            .WithStatus(CorrespondenceStatus.Published)
            .Build();
    }

    [Fact]
    public async Task Process_CorrespondenceNotFound_ReturnsEarlyWithoutEnqueuingJobs()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
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
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
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

    [Fact]
    public async Task Process_NoExistingRemindersForRecipient_CreatesNewDialogAndSavesReminder()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var newDialogId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync(newDialogId.ToString());

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _dialogportenServiceMock.Verify(
            x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()),
            Times.Once);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(
                It.Is<ConfidentialReminderEntity>(r =>
                    r.CorrespondenceId == correspondenceId &&
                    r.DialogId == newDialogId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_DialogCreationFails_StillPersistsReminder()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync(string.Empty);

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(It.IsAny<ConfidentialReminderEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_RecipientAlreadyHasReminderWithDialog_LinksToExistingDialogWithoutCreatingNew()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var existingDialogId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _confidentialReminderRepositoryMock
            .Setup(x => x.GetDialogIdOfReminderForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)existingDialogId);

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _dialogportenServiceMock.Verify(
            x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()),
            Times.Never);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(
                It.Is<ConfidentialReminderEntity>(r =>
                    r.CorrespondenceId == correspondenceId &&
                    r.DialogId == existingDialogId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_RecipientHasRemindersButNoDialogId_FallsThroughToCreateNewDialog()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var newDialogId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _confidentialReminderRepositoryMock
            .Setup(x => x.GetDialogIdOfReminderForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync(newDialogId.ToString());

        // Act
        await _handler.Process(correspondenceId, CancellationToken.None);

        // Assert
        _dialogportenServiceMock.Verify(
            x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()),
            Times.Once);
        _confidentialReminderRepositoryMock.Verify(
            x => x.AddConfidentialReminder(
                It.Is<ConfidentialReminderEntity>(r => r.DialogId == newDialogId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_UnreadCorrespondence_AlwaysEnqueuesNotificationJobAndContinuation()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var newDialogId = Guid.NewGuid();
        var correspondence = CreateUnreadCorrespondence(correspondenceId);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _dialogportenServiceMock
            .Setup(x => x.CreateConfidentialReminderDialog(It.IsAny<ConfidentialReminderDialogDto>()))
            .ReturnsAsync(newDialogId.ToString());

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
