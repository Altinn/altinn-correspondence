using Altinn.Correspondence.Application.CleanupBulkFetchStatuses;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupBulkFetchStatusesHandlerTests
{
    private static CorrespondenceStatusFetchedEntity MakeFetch(Guid corrId, Guid partyId, DateTimeOffset statusChanged)
        => new() { Id = Guid.NewGuid(), CorrespondenceId = corrId, PartyUuid = partyId, StatusChanged = statusChanged };

    private static (CleanupBulkFetchStatusesHandler handler, Mock<ICorrespondenceStatusRepository> repo) BuildHandler()
    {
        var repo = new Mock<ICorrespondenceStatusRepository>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var logger = new Mock<ILogger<CleanupBulkFetchStatusesHandler>>();
        var handler = new CleanupBulkFetchStatusesHandler(repo.Object, backgroundJobClient.Object, logger.Object);
        return (handler, repo);
    }

    // The core regression test: rows 2 and 3 (within 15s of row 1) split across a batch boundary,
    // both should be deleted.
    [Fact]
    public async Task ExecuteCleanupInBackground_DeletesBothDuplicates_WhenSplitAcrossBatchBoundary()
    {
        var corrId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var t = DateTimeOffset.UtcNow;

        var row1 = MakeFetch(corrId, partyId, t);
        var row2 = MakeFetch(corrId, partyId, t.AddSeconds(2));   // duplicate, batch 1
        var row3 = MakeFetch(corrId, partyId, t.AddSeconds(10));  // duplicate, batch 2
        var row4 = MakeFetch(corrId, partyId, t.AddSeconds(60));  // new keeper (> 15s from row1)

        var (handler, repo) = BuildHandler();

        // windowSize = 2, so batches are [row1, row2] and [row3, row4]
        repo.SetupSequence(r => r.GetBulkFetchStatusesWindowAfter(3, It.IsAny<DateTimeOffset?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceStatusFetchedEntity> { row1, row2, row3 })   // returns windowSize+1=3 so isMoreStatuses=true
            .ReturnsAsync(new List<CorrespondenceStatusFetchedEntity> { row3, row4 })          // next batch from cursor
            .ReturnsAsync(new List<CorrespondenceStatusFetchedEntity>());              // end of data

        var deleted = new List<Guid>();
        repo.Setup(r => r.DeleteBulkFetchStatus(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => deleted.Add(id))
            .Returns(Task.CompletedTask);

        await handler.ExecuteCleanupInBackground(2, CancellationToken.None);

        Assert.Contains(row2.Id, deleted); // within window in batch 1
        Assert.Contains(row3.Id, deleted); // within window across batch boundary
        Assert.DoesNotContain(row1.Id, deleted); // keeper
        Assert.DoesNotContain(row4.Id, deleted); // new keeper
    }

    // Two rows >15s apart should both be kept (different windows)
    [Fact]
    public async Task ExecuteCleanupInBackground_PreservesRowsOutsideDebounceWindow()
    {
        var corrId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var t = DateTimeOffset.UtcNow;

        var row1 = MakeFetch(corrId, partyId, t);
        var row2 = MakeFetch(corrId, partyId, t.AddSeconds(16)); // outside 15s window — new keeper

        var (handler, repo) = BuildHandler();

        repo.SetupSequence(r => r.GetBulkFetchStatusesWindowAfter(It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorrespondenceStatusFetchedEntity> { row1, row2 })
            .ReturnsAsync(new List<CorrespondenceStatusFetchedEntity>());

        var deleted = new List<Guid>();
        repo.Setup(r => r.DeleteBulkFetchStatus(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => deleted.Add(id))
            .Returns(Task.CompletedTask);

        await handler.ExecuteCleanupInBackground(100, CancellationToken.None);

        Assert.Empty(deleted);
    }
}