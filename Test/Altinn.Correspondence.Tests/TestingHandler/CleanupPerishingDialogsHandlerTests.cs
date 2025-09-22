using Altinn.Correspondence.Application.CleanupPerishingDialogs;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupPerishingDialogsHandlerTests
{
    [Fact]
    public async Task ExecuteCleanupInBackground_RemovesExpiresAtAndCountsAlreadyOk()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var c1 = new CorrespondenceEntityBuilder()
            .WithCreated(now.UtcDateTime.AddMinutes(-2))
            .WithRequestedPublishTime(now.AddMinutes(-2))
            .WithExternalReference(ReferenceType.DialogportenDialogId, "d1")
            .WithAllowSystemDeleteAfter(now.AddDays(30))
            .Build();
        var c2 = new CorrespondenceEntityBuilder()
            .WithCreated(now.UtcDateTime.AddMinutes(-1))
            .WithRequestedPublishTime(now.AddMinutes(-1))
            .WithExternalReference(ReferenceType.DialogportenDialogId, "d2")
            .WithAllowSystemDeleteAfter(now.AddDays(30))
            .Build();

        var repo = new Mock<ICorrespondenceRepository>();
        repo.SetupSequence(r => r.GetCorrespondencesWindowAfter(It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceEntity> { c1, c2 })
            .ReturnsAsync(new List<CorrespondenceEntity>());
        repo.Setup(r => r.GetCorrespondencesByIdsWithExternalReferenceAndAllowSystemDeleteAfter(
                It.IsAny<List<Guid>>(),
                It.IsAny<ReferenceType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, ReferenceType _, CancellationToken __) =>
                new List<CorrespondenceEntity> { c1, c2 }.Where(x => ids.Contains(x.Id)).ToList());

        var dialog = new Mock<IDialogportenService>();
        dialog.Setup(s => s.TryRemoveDialogExpiresAt("d1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        dialog.Setup(s => s.TryRemoveDialogExpiresAt("d2", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupPerishingDialogsHandler>>();

        var handler = new CleanupPerishingDialogsHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(100, CancellationToken.None);

        // Assert
        dialog.Verify(s => s.TryRemoveDialogExpiresAt("d1", It.IsAny<CancellationToken>()), Times.Once);
        dialog.Verify(s => s.TryRemoveDialogExpiresAt("d2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCleanupInBackground_DoesNotReprocessOrSkipItemsBetweenBatches()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var all = new List<CorrespondenceEntity>();
        for (int i = 0; i < 5; i++)
        {
            all.Add(new CorrespondenceEntityBuilder()
                .WithCreated(baseTime.AddSeconds(i).UtcDateTime)
                .WithRequestedPublishTime(baseTime.AddSeconds(i))
                .WithExternalReference(ReferenceType.DialogportenDialogId, $"d{i}")
                .WithAllowSystemDeleteAfter(baseTime.AddDays(30))
                .Build());
        }

        var repo = new Mock<ICorrespondenceRepository>();
        repo.Setup(r => r.GetCorrespondencesWindowAfter(It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
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
        repo.Setup(r => r.GetCorrespondencesByIdsWithExternalReferenceAndAllowSystemDeleteAfter(
                It.IsAny<List<Guid>>(),
                It.IsAny<ReferenceType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, ReferenceType _, CancellationToken ___) =>
                all.Where(x => ids.Contains(x.Id)).ToList());

        var processed = new HashSet<string>();
        var dialog = new Mock<IDialogportenService>();
        dialog.Setup(s => s.TryRemoveDialogExpiresAt(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string id, CancellationToken _) => { processed.Add(id); return true; });

        var bg = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupPerishingDialogsHandler>>();

        var handler = new CleanupPerishingDialogsHandler(repo.Object, dialog.Object, bg.Object, logger.Object);

        // Act
        await handler.ExecuteCleanupInBackground(2, CancellationToken.None);

        // Assert
        Assert.Equal(all.Count, processed.Count);
    }
} 