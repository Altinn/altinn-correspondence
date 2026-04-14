using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.CleanupBulkFetchStatuses;

public class CleanupBulkFetchStatusesHandler(
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupBulkFetchStatusesHandler> logger) : IHandler<CleanupBulkFetchStatusesRequest, CleanupBulkFetchStatusesResponse>
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(15);

    public Task<OneOf<CleanupBulkFetchStatusesResponse, Error>> Process(CleanupBulkFetchStatusesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of bulk fetch statuses with window size {windowSize}", request.WindowSize);

        var jobId = backgroundJobClient.Enqueue(() => ExecuteCleanupInBackground(request.WindowSize, CancellationToken.None));

        logger.LogInformation("Cleanup job {jobId} has been enqueued", jobId);

        return Task.FromResult<OneOf<CleanupBulkFetchStatusesResponse, Error>>(new CleanupBulkFetchStatusesResponse
        {
            JobId = jobId,
            Message = "Cleanup job has been Enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 43200)]
    public async Task ExecuteCleanupInBackground(int windowSize, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing cleanup of bulk fetch statuses in background job");
        var totalDeleted = 0;
        var totalErrors = 0;

        try
        {
            DateTimeOffset? lastStatusChanged = null;
            Guid? lastId = null;
            bool isMoreStatuses = true;

            var keeperState = new Dictionary<(Guid, Guid), CorrespondenceStatusFetchedEntity>();

            while (isMoreStatuses)
            {
                logger.LogInformation("Processing batch starting after cursor {statusId}", lastId);
                var statusesWindow = await correspondenceStatusRepository.GetBulkFetchStatusesWindowAfter(
                    windowSize + 1,
                    lastStatusChanged,
                    lastId,
                    cancellationToken);

                isMoreStatuses = statusesWindow.Count > windowSize;
                var batch = statusesWindow.Take(windowSize).ToList();

                var duplicates = FindDuplicatesWithinDebounceWindow(batch, keeperState);

                if (batch.Count > 0)
                {
                    var last = batch[^1];
                    lastStatusChanged = last.StatusChanged;
                    lastId = last.Id;

                    var evictBefore = last.StatusChanged - DebounceWindow;
                    foreach (var key in keeperState.Keys.Where(k => keeperState[k].StatusChanged < evictBefore).ToList())
                        keeperState.Remove(key);
                }
                logger.LogInformation("Found {duplicateCount} duplicate fetch statuses to delete in current batch", duplicates.Count);

                foreach (var duplicate in duplicates)
                {
                    try
                    {
                        logger.LogInformation("Deleting duplicate bulk fetch status {StatusId} for CorrespondenceId {CorrespondenceId} and PartyUuid {PartyUuid} with StatusChanged {StatusChanged}", duplicate.Id, duplicate.CorrespondenceId, duplicate.PartyUuid, duplicate.StatusChanged);
                        await correspondenceStatusRepository.DeleteBulkFetchStatus(duplicate.Id, cancellationToken);
                        totalDeleted++;
                    }
                    catch (Exception ex)
                    {
                        totalErrors++;
                        logger.LogError(ex, "Failed to delete bulk fetch status {StatusId} for CorrespondenceId {CorrespondenceId} and PartyUuid {PartyUuid} with StatusChanged {StatusChanged}", duplicate.Id, duplicate.CorrespondenceId, duplicate.PartyUuid, duplicate.StatusChanged);
                    }
                }

                if (batch.Count == 0)
                {
                    isMoreStatuses = false;
                }
            }

            logger.LogInformation("Background cleanup completed. Total deleted: {deletedCount}, Total errors: {errorCount}", totalDeleted, totalErrors);

            if (totalErrors > 0)
            {
                logger.LogWarning("Cleanup completed with {errorCount} errors", totalErrors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cleanup of bulk fetch statuses");
            throw;
        }
    }

    // Returns statuses that are duplicates: for each (CorrespondenceId, PartyUuid) group, keep the
    // earliest entry and mark every subsequent entry within the debounce window of the keeper for deletion.
    // keeperState carries the last keeper per group across batch boundaries.
    private static List<CorrespondenceStatusFetchedEntity> FindDuplicatesWithinDebounceWindow(
        List<CorrespondenceStatusFetchedEntity> statuses,
        Dictionary<(Guid, Guid), CorrespondenceStatusFetchedEntity> keeperState)
    {
        var duplicates = new List<CorrespondenceStatusFetchedEntity>();

        var groups = statuses
            .GroupBy(s => (s.CorrespondenceId, s.PartyUuid));

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(s => s.StatusChanged).ThenBy(s => s.Id).ToList();
            var keeper = keeperState.TryGetValue(group.Key, out var previousKeeper) ? previousKeeper : ordered[0];
            var startIndex = keeperState.ContainsKey(group.Key) ? 0 : 1;

            for (int i = startIndex; i < ordered.Count; i++)
            {
                var candidate = ordered[i];
                if (candidate.StatusChanged - keeper.StatusChanged <= DebounceWindow)
                {
                    duplicates.Add(candidate);
                }
                else
                {
                    // Outside the debounce window — this becomes the new keeper
                    keeper = candidate;
                }
            }

            keeperState[group.Key] = keeper;
        }

        return duplicates;
    }
}
