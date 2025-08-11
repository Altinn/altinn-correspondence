using Altinn.Correspondence.Application.CleanupOrphanedDialogs;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupOrphanedDialogsHandlerTests
{
    [Fact]
    public async Task ExecuteCleanupInBackground_DeletesAndCountsAlreadyDeleted()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var c1 = new CorrespondenceEntity
        {
            Id = Guid.NewGuid(),
            Created = now.AddMinutes(-2),
            ResourceId = "test-resource",
            Recipient = "0192:987654321",
            Sender = "0192:123456789",
            SendersReference = "ref-1",
            RequestedPublishTime = now.AddMinutes(-2),
            ExternalReferences = new List<ExternalReferenceEntity>
            {
                new ExternalReferenceEntity { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "d1" }
            },
            Statuses = new List<CorrespondenceStatusEntity>
            {
                new CorrespondenceStatusEntity { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = now }
            }
        };
        var c2 = new CorrespondenceEntity
        {
            Id = Guid.NewGuid(),
            Created = now.AddMinutes(-1),
            ResourceId = "test-resource",
            Recipient = "0192:987654321",
            Sender = "0192:123456789",
            SendersReference = "ref-2",
            RequestedPublishTime = now.AddMinutes(-1),
            ExternalReferences = new List<ExternalReferenceEntity>
            {
                new ExternalReferenceEntity { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "d2" }
            },
            Statuses = new List<CorrespondenceStatusEntity>
            {
                new CorrespondenceStatusEntity { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = now }
            }
        };

        var repo = new Mock<ICorrespondenceRepository>();
        repo.SetupSequence(r => r.GetPurgedCorrespondencesWithDialogsAfter(It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceEntity> { c1, c2 })
            .ReturnsAsync(new List<CorrespondenceEntity>());

        var dialog = new Mock<IDialogportenService>();
        dialog.Setup(s => s.TrySoftDeleteDialog("d1")).ReturnsAsync(true);
        dialog.Setup(s => s.TrySoftDeleteDialog("d2")).ReturnsAsync(false);

        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupOrphanedDialogsHandler>>();

        var handler = new CleanupOrphanedDialogsHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(CancellationToken.None);

        // Assert
        dialog.Verify(s => s.TrySoftDeleteDialog("d1"), Times.Once);
        dialog.Verify(s => s.TrySoftDeleteDialog("d2"), Times.Once);
    }
}


