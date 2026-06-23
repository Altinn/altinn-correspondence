using Altinn.Correspondence.Application.BatchJobs;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MakeCorrespondenceAvailableBatchJob(
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient)
{
    public const int BatchSize = 999;

    public ChainedBatchJobDefinition<MakeCorrespondenceAvailableRequest, CorrespondenceEntity> CreateDefinition() =>
        new()
        {
            Settings = new ChainedBatchJobSettings
            {
                JobName = "MakeCorrespondenceAvailable",
                BatchSize = BatchSize,
                BackpressureLimit = BatchSize * 20,
            },
            FetchBatchAsync = async (request, cancellationToken) =>
            {
                var items = await correspondenceRepository.GetCandidatesForMigrationToDialogporten(
                    BatchSize,
                    request.CursorCreated,
                    request.CursorId,
                    request.CreatedFrom,
                    request.CreatedTo,
                    cancellationToken);
                return new ChainedBatchJobFetchResult<CorrespondenceEntity>(items, items.Count == BatchSize);
            },
            GetCursorFromItem = correspondence => new KeysetCursor(correspondence.Created, correspondence.Id),
            EnqueueWorkerJob = (correspondence, request) =>
                backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(
                    ChainedBatchJobQueues.Worker,
                    handler => handler.MakeCorrespondenceAvailableInDialogportenAndApi(
                        correspondence.Id,
                        CancellationToken.None,
                        true,
                        null,
                        request.CreateEvents)),
            CreateNextState = (request, cursor, fetchedCount) => new MakeCorrespondenceAvailableRequest
            {
                AsyncProcessing = true,
                BatchSize = request.BatchSize - fetchedCount,
                CreateEvents = request.CreateEvents,
                CursorCreated = cursor.Created,
                CursorId = cursor.Id,
                CreatedFrom = request.CreatedFrom,
                CreatedTo = request.CreatedTo,
            },
            EnqueueNextBatch = nextState =>
                backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(
                    ChainedBatchJobQueues.Orchestrator,
                    handler => handler.MakeCorrespondenceAvailable(nextState, CancellationToken.None)),
            RescheduleBatch = state =>
                backgroundJobClient.Schedule<MigrateCorrespondenceHandler>(
                    ChainedBatchJobQueues.Orchestrator,
                    handler => handler.MakeCorrespondenceAvailable(state, CancellationToken.None),
                    TimeSpan.FromMinutes(1)),
        };
}
