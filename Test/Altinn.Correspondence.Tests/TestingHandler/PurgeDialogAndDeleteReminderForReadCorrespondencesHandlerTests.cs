using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeDialogAndDeleteReminderForReadCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class PurgeDialogAndDeleteReminderForReadCorrespondencesHandlerTests
{
    private readonly Mock<IConfidentialReminderRepository> _confidentialReminderRepositoryMock = new();
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock = new();
    private readonly Mock<IDialogportenService> _dialogportenServiceMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<IConfidentialReminderDialogSynchronizer> _dialogSynchronizerMock = new();
    private readonly PurgeDialogAndDeleteReminderForReadCorrespondencesHandler _handler;

    public PurgeDialogAndDeleteReminderForReadCorrespondencesHandlerTests()
    {
        _dialogSynchronizerMock
            .Setup(x => x.ExecuteForRecipientAsync(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));

        _handler = new PurgeDialogAndDeleteReminderForReadCorrespondencesHandler(
            _confidentialReminderRepositoryMock.Object,
            _idempotencyKeyRepositoryMock.Object,
            _dialogportenServiceMock.Object,
            _backgroundJobClientMock.Object,
            _dialogSynchronizerMock.Object,
            Mock.Of<ILogger<PurgeDialogAndDeleteReminderForReadCorrespondencesHandler>>());
    }

    [Fact]
    public async Task ExecuteDeleteInBackground_WhenStaleReminderAlreadyRemovedUnderLock_DoesNotDeleteKeyOrDialog()
    {
        // Arrange — list contained a stale snapshot; after the recipient lock the row is gone
        // (e.g. overview cleanup interleaved). Recipient may now have a new reminder/dialog.
        var recipient = "urn:altinn:organization:identifier-no:991825827";
        var staleCorrespondenceId = Guid.NewGuid();
        var staleDialogId = Guid.NewGuid();
        var staleReminder = new ConfidentialReminderEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = staleCorrespondenceId,
            Recipient = recipient,
            DialogId = staleDialogId
        };

        _confidentialReminderRepositoryMock
            .Setup(x => x.GetConfidentialRemindersLinkedToReadCorrespondences(It.IsAny<CancellationToken>()))
            .ReturnsAsync([staleReminder]);
        _confidentialReminderRepositoryMock
            .Setup(x => x.GetByCorrespondenceId(staleCorrespondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfidentialReminderEntity?)null);
        // Would wrongly close a newer reminder session if cleanup used the stale snapshot + count.
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(recipient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.ExecuteDeleteInBackground(CancellationToken.None);

        // Assert
        _confidentialReminderRepositoryMock.Verify(
            x => x.RemoveConfidentialReminderByCorrespondenceId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _idempotencyKeyRepositoryMock.Verify(
            x => x.DeleteByPartyUrnAndTypeAsync(
                It.IsAny<string>(),
                It.IsAny<IdempotencyType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _dialogportenServiceMock.Verify(
            x => x.TrySoftDeleteDialog(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteDeleteInBackground_WhenTargetStillPersistedAndIsFinal_DeletesKeyAndSoftDeletesPersistedDialog()
    {
        // Arrange
        var recipient = "urn:altinn:organization:identifier-no:991825827";
        var correspondenceId = Guid.NewGuid();
        var dialogId = Guid.NewGuid();
        var listSnapshot = new ConfidentialReminderEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondenceId,
            Recipient = recipient,
            DialogId = Guid.NewGuid() // stale dialog id in snapshot
        };
        var persisted = new ConfidentialReminderEntity
        {
            Id = listSnapshot.Id,
            CorrespondenceId = correspondenceId,
            Recipient = recipient,
            DialogId = dialogId
        };

        _confidentialReminderRepositoryMock
            .Setup(x => x.GetConfidentialRemindersLinkedToReadCorrespondences(It.IsAny<CancellationToken>()))
            .ReturnsAsync([listSnapshot]);
        _confidentialReminderRepositoryMock
            .Setup(x => x.GetByCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(persisted);
        _confidentialReminderRepositoryMock
            .Setup(x => x.NumberOfRemindersForRecipient(recipient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.ExecuteDeleteInBackground(CancellationToken.None);

        // Assert — uses persisted DialogId, not the stale snapshot
        _confidentialReminderRepositoryMock.Verify(
            x => x.RemoveConfidentialReminderByCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()),
            Times.Once);
        _idempotencyKeyRepositoryMock.Verify(
            x => x.DeleteByPartyUrnAndTypeAsync(
                recipient,
                IdempotencyType.ConfidentialReminderDialog,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dialogportenServiceMock.Verify(
            x => x.TrySoftDeleteDialog(dialogId.ToString()),
            Times.Once);
        _dialogportenServiceMock.Verify(
            x => x.TrySoftDeleteDialog(listSnapshot.DialogId!.Value.ToString()),
            Times.Never);
    }
}
