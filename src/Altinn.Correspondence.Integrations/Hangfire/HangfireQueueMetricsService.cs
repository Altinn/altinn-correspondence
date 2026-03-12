using System.Diagnostics.Metrics;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Hosting;

namespace Altinn.Correspondence.Integrations.Hangfire;

public sealed class HangfireQueueMetricsService : IHostedService
{
    private const string MeterName = "Altinn.Correspondence.Integrations.Hangfire";
    private static readonly string[] _queues = [HangfireQueues.Default, HangfireQueues.LiveMigration, HangfireQueues.Migration];
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(30);

    private readonly Meter _meter = new(MeterName);
    private readonly Lock _snapshotLock = new();

    private DateTime _lastSnapshotAtUtc = DateTime.MinValue;
    private HangfireMetricsSnapshot _lastSnapshot = HangfireMetricsSnapshot.Empty;

    public HangfireQueueMetricsService()
    {
        _meter.CreateObservableGauge(
            name: "hangfire.jobs.enqueued",
            observeValues: ObserveEnqueuedByQueue,
            unit: "jobs",
            description: "Current number of enqueued jobs per Hangfire queue.");

        _meter.CreateObservableGauge(
            name: "hangfire.jobs.processing",
            observeValue: () => GetSnapshot().Processing,
            unit: "jobs",
            description: "Current number of Hangfire jobs in Processing state.");

        _meter.CreateObservableGauge(
            name: "hangfire.jobs.scheduled",
            observeValue: () => GetSnapshot().Scheduled,
            unit: "jobs",
            description: "Current number of Hangfire jobs in Scheduled state.");

        _meter.CreateObservableGauge(
            name: "hangfire.jobs.failed",
            observeValue: () => GetSnapshot().Failed,
            unit: "jobs",
            description: "Current number of Hangfire jobs in Failed state.");

        _meter.CreateObservableGauge(
            name: "hangfire.servers.active",
            observeValue: () => GetSnapshot().ActiveServers,
            unit: "servers",
            description: "Current number of active Hangfire servers.");

        _meter.CreateObservableGauge(
            name: "hangfire.component.healthy",
            observeValue: () => GetSnapshot().ComponentHealthy,
            unit: "state",
            description: "1 when at least one Hangfire server is active, otherwise 0.");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = GetSnapshot();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _meter.Dispose();
        return Task.CompletedTask;
    }

    private IEnumerable<Measurement<long>> ObserveEnqueuedByQueue()
    {
        var snapshot = GetSnapshot();
        foreach (var queue in _queues)
        {
            if (snapshot.EnqueuedByQueue.TryGetValue(queue, out var value))
            {
                yield return new Measurement<long>(value, new KeyValuePair<string, object?>("queue", queue));
            }
        }
    }

    private HangfireMetricsSnapshot GetSnapshot()
    {
        lock (_snapshotLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastSnapshotAtUtc < SnapshotTtl)
            {
                return _lastSnapshot;
            }

            var monitoringApi = JobStorage.Current.GetMonitoringApi();
            var activeServers = GetActiveServerCount(monitoringApi);
            var snapshot = new HangfireMetricsSnapshot(
                EnqueuedByQueue: GetEnqueuedByQueue(monitoringApi),
                Processing: monitoringApi.ProcessingCount(),
                Scheduled: monitoringApi.ScheduledCount(),
                Failed: monitoringApi.FailedCount(),
                ActiveServers: activeServers,
                ComponentHealthy: activeServers > 0 ? 1 : 0);

            _lastSnapshot = snapshot;
            _lastSnapshotAtUtc = now;
            return snapshot;
        }
    }

    private static IReadOnlyDictionary<string, long> GetEnqueuedByQueue(IMonitoringApi monitoringApi)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var queue in _queues)
        {
            result[queue] = monitoringApi.EnqueuedCount(queue);
        }

        return result;
    }

    private static long GetActiveServerCount(IMonitoringApi monitoringApi)
    {
        var servers = monitoringApi.Servers();
        return servers?.Count ?? 0;
    }

    private sealed record HangfireMetricsSnapshot(
        IReadOnlyDictionary<string, long> EnqueuedByQueue,
        long Processing,
        long Scheduled,
        long Failed,
        long ActiveServers,
        long ComponentHealthy)
    {
        public static readonly HangfireMetricsSnapshot Empty = new(
            EnqueuedByQueue: new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
            Processing: 0,
            Scheduled: 0,
            Failed: 0,
            ActiveServers: 0,
            ComponentHealthy: 0);
    }
}
