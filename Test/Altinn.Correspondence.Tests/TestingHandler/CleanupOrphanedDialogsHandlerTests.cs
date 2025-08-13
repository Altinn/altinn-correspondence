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
        await handler.ExecuteCleanupInBackground(100, CancellationToken.None);

        // Assert
        dialog.Verify(s => s.TrySoftDeleteDialog("d1"), Times.Once);
        dialog.Verify(s => s.TrySoftDeleteDialog("d2"), Times.Once);
    }

    [Fact]
    public async Task ExecuteCleanupInBackground_DoesNotReprocessOrSkipItemsBetweenBatches()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var all = new List<CorrespondenceEntity>();
        for (int i = 0; i < 5; i++)
        {
            all.Add(new CorrespondenceEntity
            {
                Id = Guid.NewGuid(),
                Created = baseTime.AddSeconds(i),
                Recipient = "0192:987654321",
                RequestedPublishTime = baseTime.AddSeconds(i),
                ResourceId = "r",
                Sender = "0192:123456789",
                SendersReference = i.ToString(),
                Statuses = new List<CorrespondenceStatusEntity> { new() { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime.AddSeconds(i) } },
                ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = $"d{i}" } }
            });
        }

        var repo = new Mock<ICorrespondenceRepository>();
        repo.Setup(r => r.GetPurgedCorrespondencesWithDialogsAfter(It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int limit, DateTimeOffset? lastCreated, Guid? lastId, bool _, CancellationToken __) =>
            {
                var query = all
                    .Where(c => !lastCreated.HasValue
                        || c.Created > lastCreated.Value
                        || (c.Created == lastCreated.Value && lastId.HasValue && c.Id.CompareTo(lastId.Value) > 0))
                    .OrderBy(c => c.Created).ThenBy(c => c.Id)
                    .Take(limit)
                    .ToList();
                return query;
            });

        var processed = new HashSet<string>();
        var dialog = new Mock<IDialogportenService>();
        dialog.Setup(s => s.TrySoftDeleteDialog(It.IsAny<string>()))
              .ReturnsAsync((string id) => { processed.Add(id); return true; });

        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupOrphanedDialogsHandler>>();

        var handler = new CleanupOrphanedDialogsHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(2, CancellationToken.None);

        // Assert
        Assert.Equal(all.Count, processed.Count);
    }
}


