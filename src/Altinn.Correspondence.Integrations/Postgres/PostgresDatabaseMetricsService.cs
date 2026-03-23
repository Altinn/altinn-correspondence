using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Correspondence.Integrations.Postgres;

public sealed class PostgresDatabaseMetricsService : IHostedService
{
    private const string MeterName = "Altinn.Correspondence.Integrations.Postgres";
    private const string DatabaseNameTag = "database.name";
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(30);
    private readonly Meter _meter = new(MeterName);
    private readonly Lock _snapshotLock = new();

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresDatabaseMetricsService> _logger;

    private DateTime _lastSnapshotAtUtc = DateTime.MinValue;
    private PostgresDatabaseMetricsSnapshot _lastSnapshot = PostgresDatabaseMetricsSnapshot.Empty;

    public PostgresDatabaseMetricsService(
        NpgsqlDataSource dataSource,
        ILogger<PostgresDatabaseMetricsService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;

        _meter.CreateObservableGauge(
            name: "postgres.database.temp_files",
            observeValues: () => ObserveLong(snapshot => snapshot.TempFiles),
            unit: "files",
            description: "Cumulative number of temporary files created by the current PostgreSQL database.");

        _meter.CreateObservableGauge(
            name: "postgres.database.temp_mb_written",
            observeValues: () => ObserveDouble(snapshot => snapshot.TempMbWritten),
            unit: "MB",
            description: "Cumulative amount of temporary data written by the current PostgreSQL database.");

        _meter.CreateObservableGauge(
            name: "postgres.database.blk_read_time",
            observeValues: () => ObserveDouble(snapshot => snapshot.BlockReadTimeMilliseconds),
            unit: "ms",
            description: "Cumulative time spent reading data file blocks by the current PostgreSQL database.");

        _meter.CreateObservableGauge(
            name: "postgres.database.blk_write_time",
            observeValues: () => ObserveDouble(snapshot => snapshot.BlockWriteTimeMilliseconds),
            unit: "ms",
            description: "Cumulative time spent writing data file blocks by the current PostgreSQL database.");

        _meter.CreateObservableGauge(
            name: "postgres.database.deadlocks",
            observeValues: () => ObserveLong(snapshot => snapshot.Deadlocks),
            unit: "deadlocks",
            description: "Cumulative number of deadlocks detected in the current PostgreSQL database.");

        _meter.CreateObservableGauge(
            name: "postgres.database.stats_reset_unix_time",
            observeValues: () => ObserveDouble(snapshot => snapshot.StatsResetUnixTimeSeconds),
            unit: "s",
            description: "Unix timestamp for the last reset of statistics in the current PostgreSQL database.");

        _meter.CreateObservableGauge(
            name: "postgres.database.component_healthy",
            observeValues: () => ObserveLong(snapshot => snapshot.ComponentHealthy),
            unit: "state",
            description: "1 when PostgreSQL statistics for the current database can be read, otherwise 0.");
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

    private IEnumerable<Measurement<long>> ObserveLong(Func<PostgresDatabaseMetricsSnapshot, long> selector)
    {
        var snapshot = GetSnapshot();
        yield return new Measurement<long>(selector(snapshot), GetTags(snapshot.DatabaseName));
    }

    private IEnumerable<Measurement<double>> ObserveDouble(Func<PostgresDatabaseMetricsSnapshot, double> selector)
    {
        var snapshot = GetSnapshot();
        yield return new Measurement<double>(selector(snapshot), GetTags(snapshot.DatabaseName));
    }

    private static KeyValuePair<string, object?>[] GetTags(string databaseName) =>
        string.IsNullOrWhiteSpace(databaseName)
            ? []
            : [new KeyValuePair<string, object?>(DatabaseNameTag, databaseName)];

    private PostgresDatabaseMetricsSnapshot GetSnapshot()
    {
        lock (_snapshotLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastSnapshotAtUtc < SnapshotTtl)
            {
                return _lastSnapshot;
            }

            try
            {
                _lastSnapshot = ReadSnapshot();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to read PostgreSQL database metrics from pg_stat_database");
                _lastSnapshot = _lastSnapshot with { ComponentHealthy = 0 };
            }

            _lastSnapshotAtUtc = now;
            return _lastSnapshot;
        }
    }

    private PostgresDatabaseMetricsSnapshot ReadSnapshot()
    {
        using var connection = _dataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                datname,
                temp_files,
                temp_bytes,
                COALESCE(blk_read_time, 0),
                COALESCE(blk_write_time, 0),
                deadlocks,
                COALESCE(EXTRACT(EPOCH FROM stats_reset), 0)
            FROM pg_stat_database
            WHERE datname = current_database();
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            _logger.LogWarning("No pg_stat_database row was returned for the current database");
            return PostgresDatabaseMetricsSnapshot.Empty;
        }

        var databaseName = reader.GetString(0);
        var tempFiles = reader.GetInt64(1);
        var tempBytes = reader.GetInt64(2);
        var blockReadTimeMilliseconds = reader.GetDouble(3);
        var blockWriteTimeMilliseconds = reader.GetDouble(4);
        var deadlocks = reader.GetInt64(5);
        var statsResetUnixTimeSeconds = reader.GetDouble(6);

        return new PostgresDatabaseMetricsSnapshot(
            DatabaseName: databaseName,
            TempFiles: tempFiles,
            TempMbWritten: tempBytes / 1024d / 1024d,
            BlockReadTimeMilliseconds: blockReadTimeMilliseconds,
            BlockWriteTimeMilliseconds: blockWriteTimeMilliseconds,
            Deadlocks: deadlocks,
            StatsResetUnixTimeSeconds: statsResetUnixTimeSeconds,
            ComponentHealthy: 1);
    }

    private sealed record PostgresDatabaseMetricsSnapshot(
        string DatabaseName,
        long TempFiles,
        double TempMbWritten,
        double BlockReadTimeMilliseconds,
        double BlockWriteTimeMilliseconds,
        long Deadlocks,
        double StatsResetUnixTimeSeconds,
        long ComponentHealthy)
    {
        public static readonly PostgresDatabaseMetricsSnapshot Empty = new(
            DatabaseName: string.Empty,
            TempFiles: 0,
            TempMbWritten: 0,
            BlockReadTimeMilliseconds: 0,
            BlockWriteTimeMilliseconds: 0,
            Deadlocks: 0,
            StatsResetUnixTimeSeconds: 0,
            ComponentHealthy: 0);
    }
}
