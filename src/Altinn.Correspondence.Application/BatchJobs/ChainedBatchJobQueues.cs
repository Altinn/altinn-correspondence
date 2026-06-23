using Altinn.Correspondence.Integrations.Hangfire;

namespace Altinn.Correspondence.Application.BatchJobs;

/// <summary>
/// Standard Hangfire queue assignment for chained batch jobs.
/// Orchestrator jobs are lightweight (query + enqueue) and run on live-migration,
/// which is prioritized ahead of migration on the migration Hangfire server.
/// Per-entity worker jobs run on migration.
/// </summary>
public static class ChainedBatchJobQueues
{
    public const string Orchestrator = HangfireQueues.LiveMigration;
    public const string Worker = HangfireQueues.Migration;
}
