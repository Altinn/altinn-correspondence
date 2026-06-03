using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Correspondence.DialogActivityExporter;

public class DialogActivityExportService
{
    private readonly string _connectionString;
    private readonly int _batchSize;
    private readonly ILogger<DialogActivityExportService> _logger;
    private bool _isTestMode; // Track test mode for query logging

    public DialogActivityExportService(
        string connectionString,
        int batchSize,
        ILogger<DialogActivityExportService> logger)
    {
        _connectionString = connectionString;
        _batchSize = batchSize;
        _logger = logger;
    }

    public async Task ExportToCSVAsync(
        string outputFilePath,
        int issueNumber,
        DateTime cutoffTimestamp,
        long preCalculatedCount = 0,
        int? maxBatches = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalProcessed = 0L;
        var batchNumber = 0;
        (Guid correspondenceId, int status)? lastCursor = null;

        // Set test mode for query logging
        _isTestMode = maxBatches.HasValue;

        _logger.LogInformation("Starting export for Issue #{Issue} to {FilePath}", issueNumber, outputFilePath);
        if (maxBatches.HasValue)
        {
            _logger.LogInformation("TEST MODE: Limited to {MaxBatches} batch(es)", maxBatches.Value);
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Use pre-calculated count if available (for progress percentage)
        // If not available (0), we'll just show total processed without percentage
        long totalCount = preCalculatedCount;
        if (totalCount > 0)
        {
            _logger.LogInformation("Expected records (from pre-calculated count): ~{Count:N0}", totalCount);
        }
        else
        {
            _logger.LogInformation("Total count not available - will track processed records only");
        }

        // Create output file
        await using var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536);
        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, bufferSize: 65536);

        // Write CSV header
        await writer.WriteLineAsync("DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            batchNumber++;
            var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var (batchCount, newCursor) = await ProcessBatchAsync(
                connection,
                writer,
                issueNumber,
                cutoffTimestamp,
                lastCursor,
                cancellationToken);

            if (batchCount == 0)
                break;

            totalProcessed += batchCount;
            lastCursor = newCursor;
            batchStopwatch.Stop();

            _logger.LogDebug(
                "Batch {BatchNumber}: {BatchCount:N0} rows in {Elapsed}ms",
                batchNumber, batchCount, batchStopwatch.ElapsedMilliseconds);

            progress?.Report(new ExportProgress
            {
                TotalProcessed = totalProcessed,
                TotalCount = totalCount,
                BatchNumber = batchNumber,
                ElapsedTime = stopwatch.Elapsed
            });

            if (batchCount < _batchSize)
                break;

            // Check if we've reached the max batch limit (test mode)
            if (maxBatches.HasValue && batchNumber >= maxBatches.Value)
            {
                _logger.LogInformation("Reached max batch limit ({MaxBatches}). Stopping test export.", maxBatches.Value);
                break;
            }
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Export completed. Total: {Total:N0} rows in {Elapsed}",
            totalProcessed, stopwatch.Elapsed);
    }

    public async Task ExportBothToCSVAsync(
        string outputFilePath,
        DateTime cutoffTimestamp,
        long preCalculatedCount1716 = 0,
        long preCalculatedCount1951 = 0,
        int? maxBatches = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Set test mode for query logging
        _isTestMode = maxBatches.HasValue;

        _logger.LogInformation("Starting combined export for both issues to {FilePath}", outputFilePath);
        if (maxBatches.HasValue)
        {
            _logger.LogInformation("TEST MODE: Limited to {MaxBatches} batch(es) per issue", maxBatches.Value);
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Use pre-calculated counts if available (for progress percentage)
        long count1716 = preCalculatedCount1716;
        long count1951 = preCalculatedCount1951;

        // Only calculate total if both counts are available
        long totalCount = (count1716 > 0 && count1951 > 0) ? count1716 + count1951 : 0;

        // Only consider total known if both individual counts are available
        if (totalCount > 0)
        {
            _logger.LogInformation("Expected records (from pre-calculated counts): ~{Count:N0} total (1716: {Count1716:N0}, 1951: {Count1951:N0})", 
                totalCount, count1716, count1951);
        }
        else
        {
            _logger.LogInformation("Total count not available - will track processed records only");
        }

        // Create output file
        await using var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536);
        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, bufferSize: 65536);

        // Write CSV header
        await writer.WriteLineAsync("DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var totalProcessed = 0L;

        // Export Issue #1716 first (smaller dataset)
        _logger.LogInformation("Exporting Issue #1716...");
        var processed1716 = await ExportIssueToWriter(
            connection, writer, 1716, cutoffTimestamp, 
            count1716, totalProcessed, totalCount, maxBatches, progress, stopwatch, cancellationToken);
        totalProcessed += processed1716;

        // Export Issue #1951
        _logger.LogInformation("Exporting Issue #1951...");
        var processed1951 = await ExportIssueToWriter(
            connection, writer, 1951, cutoffTimestamp,
            count1951, totalProcessed, totalCount, maxBatches, progress, stopwatch, cancellationToken);
        totalProcessed += processed1951;

        stopwatch.Stop();
        _logger.LogInformation(
            "Combined export completed. Total: {Total:N0} rows (1716: {Count1716:N0}, 1951: {Count1951:N0}) in {Elapsed}",
            totalProcessed, processed1716, processed1951, stopwatch.Elapsed);
    }

    private async Task<long> ExportIssueToWriter(
        NpgsqlConnection connection,
        StreamWriter writer,
        int issueNumber,
        DateTime cutoffTimestamp,
        long issueTotal,
        long currentTotal,
        long grandTotal,
        int? maxBatches,
        IProgress<ExportProgress>? progress,
        System.Diagnostics.Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var totalProcessed = 0L;
        var batchNumber = 0;
        (Guid correspondenceId, int status)? lastCursor = null;

        while (true)
        {
            batchNumber++;

            var (batchCount, newCursor) = await ProcessBatchAsync(
                connection,
                writer,
                issueNumber,
                cutoffTimestamp,
                lastCursor,
                cancellationToken);

            if (batchCount == 0)
                break;

            totalProcessed += batchCount;
            lastCursor = newCursor;

            // Report progress with combined totals
            progress?.Report(new ExportProgress
            {
                TotalProcessed = currentTotal + totalProcessed,
                TotalCount = grandTotal,
                BatchNumber = batchNumber,
                ElapsedTime = stopwatch.Elapsed
            });

            if (batchCount < _batchSize)
                break;

            // Check if we've reached the max batch limit (test mode)
            if (maxBatches.HasValue && batchNumber >= maxBatches.Value)
            {
                _logger.LogInformation("Issue #{Issue}: Reached max batch limit ({MaxBatches}). Stopping test export.", issueNumber, maxBatches.Value);
                break;
            }
        }

        return totalProcessed;
    }

    private async Task<long> GetTotalCountAsync(
        NpgsqlConnection connection,
        int issueNumber,
        DateTime cutoffTimestamp,
        CancellationToken cancellationToken)
    {
        var (syncFilter, timestampFilter) = GetFiltersForIssue(issueNumber);

        var countQuery = $@"
            SELECT COUNT(*)
            FROM correspondence.""CorrespondenceStatuses"" stats
            INNER JOIN correspondence.""Correspondences"" corr 
                ON stats.""CorrespondenceId"" = corr.""Id"" 
                AND corr.""Altinn2CorrespondenceId"" IS NOT NULL 
                AND corr.""IsMigrating"" = FALSE
                AND {syncFilter}
            INNER JOIN correspondence.""A2Parties"" ap 
                ON stats.""PartyUuid"" = ap.""PartyUuid""
                AND corr.""Recipient"" <> ap.""RecipientUrn""
            WHERE stats.""Status"" IN (4, 6)
              {timestampFilter}";

        await using var cmd = new NpgsqlCommand(countQuery, connection);
        cmd.Parameters.AddWithValue("cutoffTimestamp", cutoffTimestamp);
        cmd.CommandTimeout = 120;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private async Task<(int Count, (Guid correspondenceId, int status)? LastCursor)> ProcessBatchAsync(
        NpgsqlConnection connection,
        StreamWriter writer,
        int issueNumber,
        DateTime cutoffTimestamp,
        (Guid correspondenceId, int status)? lastCursor,
        CancellationToken cancellationToken)
    {
        // OPTIMIZATION: Run Status 4 and Status 6 as separate queries and merge in-memory
        // This avoids the slow UNION ALL + ORDER BY performance issue
        // Status 4: ~21ms, Status 6: ~3s vs UNION ALL + ORDER BY: 12+ minutes

        var status4Results = await FetchStatusRecordsAsync(
            connection, issueNumber, cutoffTimestamp,
            statusValue: 4, lastCursor, cancellationToken);

        var status6Results = await FetchStatusRecordsAsync(
            connection, issueNumber, cutoffTimestamp,
            statusValue: 6, lastCursor, cancellationToken);

        // Merge and sort in-memory (much faster than database ORDER BY on UNION ALL)
        var allResults = status4Results
            .Concat(status6Results)
            .OrderBy(r => r.CorrespondenceId)
            .ThenBy(r => r.Status)
            .Take(_batchSize)
            .ToList();

        // Write to CSV
        foreach (var record in allResults)
        {
            var line = FormatCSVLine(
                EscapeCSV(record.DialogId),
                EscapeCSV(record.DialogActivityId),
                EscapeCSV(record.Timestamp.ToString("o", CultureInfo.InvariantCulture)),
                EscapeCSV(record.ActorId),
                EscapeCSV(record.ActorName),
                EscapeCSV(record.ActivityType)
            );
            await writer.WriteLineAsync(line);
        }

        // Flush to disk after each batch to ensure data is written immediately
        await writer.FlushAsync();

        var lastProcessedCursor = allResults.Count > 0 
            ? (allResults[^1].CorrespondenceId, allResults[^1].Status) 
            : ((Guid correspondenceId, int status)?)null;

        return (allResults.Count, lastProcessedCursor);
    }

    private async Task<List<DialogActivityRecord>> FetchStatusRecordsAsync(
        NpgsqlConnection connection,
        int issueNumber,
        DateTime cutoffTimestamp,
        int statusValue,
        (Guid correspondenceId, int status)? lastCursor,
        CancellationToken cancellationToken)
    {
        var (syncFilter, timestampFilter) = GetFiltersForIssue(issueNumber);

        var activityType = statusValue == 4 ? "CorrespondenceOpened" : "CorrespondenceConfirmed";
        var idcJoinAlias = statusValue == 4 ? "idcFetch" : "idcConfirm";

        // Map CorrespondenceStatus to StatusAction:
        // Status 4 (Read) -> StatusAction 3 (Fetched)
        // Status 6 (Confirmed) -> StatusAction 6 (Confirmed)
        // NOTE: StatusAction is stored as TEXT in database, so we use string comparison
        var statusActionValue = statusValue == 4 ? "3" : "6";

        // NOTE: ORDER BY matches cursor predicate for deterministic pagination
        // This ensures each batch returns consistent, non-overlapping results
        var query = $@"
            SELECT 
                er.""ReferenceValue"" AS DialogId,
                {idcJoinAlias}.""Id"" AS DialogActivityId,
                stats.""CorrespondenceId"",
                stats.""StatusChanged"" AS Timestamp,
                ap.""OutputActorId"" AS ActorId,
                ap.""Name"" AS ActorName,
                {statusValue} AS Status,
                '{activityType}' AS ActivityType
            FROM correspondence.""CorrespondenceStatuses"" stats
            INNER JOIN correspondence.""Correspondences"" corr 
                ON stats.""CorrespondenceId"" = corr.""Id"" 
                AND corr.""Altinn2CorrespondenceId"" IS NOT NULL 
                AND corr.""IsMigrating"" = FALSE
                AND {syncFilter}
            INNER JOIN correspondence.""A2Parties"" ap 
                ON stats.""PartyUuid"" = ap.""PartyUuid""
                AND corr.""Recipient"" <> ap.""RecipientUrn""
            INNER JOIN correspondence.""ExternalReferences"" er
                ON stats.""CorrespondenceId"" = er.""CorrespondenceId"" 
                AND er.""ReferenceType"" = 3
            INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
                ON stats.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId"" 
                AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
            WHERE stats.""Status"" = {statusValue}
              {timestampFilter}
              AND (@lastId IS NULL OR (stats.""CorrespondenceId"", stats.""Status"") > (@lastId, @lastStatus))
            ORDER BY stats.""CorrespondenceId"", stats.""Status""
            LIMIT @fetchLimit";

        await using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("cutoffTimestamp", cutoffTimestamp);

        // Explicitly specify parameter types for nullable cursor values
        var lastIdParam = new NpgsqlParameter("lastId", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = lastCursor.HasValue ? lastCursor.Value.correspondenceId : (object)DBNull.Value
        };
        cmd.Parameters.Add(lastIdParam);

        var lastStatusParam = new NpgsqlParameter("lastStatus", NpgsqlTypes.NpgsqlDbType.Integer)
        {
            Value = lastCursor.HasValue ? lastCursor.Value.status : (object)DBNull.Value
        };
        cmd.Parameters.Add(lastStatusParam);

        cmd.Parameters.AddWithValue("fetchLimit", _batchSize); // Fetch up to batchSize from each status

        cmd.CommandTimeout = 300;

        // Log query in test mode for verification
        if (_isTestMode)
        {
            var logQuery = query
                .Replace("@cutoffTimestamp", $"'{cutoffTimestamp:yyyy-MM-dd HH:mm:ss}'")
                .Replace("@lastId", lastCursor.HasValue ? $"'{lastCursor.Value.correspondenceId}'" : "NULL")
                .Replace("@lastStatus", lastCursor.HasValue ? lastCursor.Value.status.ToString() : "NULL")
                .Replace("@fetchLimit", _batchSize.ToString());

            _logger.LogInformation("TEST MODE - Executing query for Status {StatusValue}:", statusValue);
            _logger.LogInformation("{Query}", logQuery);
        }

        var results = new List<DialogActivityRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DialogActivityRecord
            {
                DialogId = reader.GetString(0),
                DialogActivityId = reader.GetGuid(1).ToString(),
                CorrespondenceId = reader.GetGuid(2),
                Timestamp = reader.GetDateTime(3),
                ActorId = reader.GetString(4),
                ActorName = reader.GetString(5),
                Status = reader.GetInt32(6),
                ActivityType = reader.GetString(7)
            });
        }

        return results;
    }

    private static (string SyncFilter, string TimestampFilter) GetFiltersForIssue(
        int issueNumber)
    {
        return issueNumber switch
        {
            // Issue #1951: Migrated Events (NOT Synced from Altinn2)
            // Records: ~150 million
            // Uses StatusChanged BETWEEN for better index selectivity on large dataset
            // OPTIMIZATION: corr.Created filter removed (caused 3s → 12+ min degradation)
            1951 => (
                "stats.\"SyncedFromAltinn2\" IS NULL",
                "AND stats.\"StatusChanged\" BETWEEN '2019-03-23 00:00:00' AND @cutoffTimestamp"
            ),
            // Issue #1716: Synced Events from Altinn2
            // Records: ~7-9 million
            // Uses SyncedFromAltinn2 < cutoff (simple less-than on smaller dataset)
            1716 => (
                "stats.\"SyncedFromAltinn2\" IS NOT NULL",
                "AND stats.\"SyncedFromAltinn2\" < @cutoffTimestamp"
            ),
            _ => throw new ArgumentException($"Invalid issue number: {issueNumber}")
        };
    }

    private static string FormatCSVLine(params string[] fields)
    {
        return string.Join(",", fields.Select(f => $"\"{f}\""));
    }

    private static string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\"", "\"\"");
    }

    // Record to hold query results before CSV export
    internal record DialogActivityRecord
    {
        public required string DialogId { get; init; }
        public required string DialogActivityId { get; init; }
        public required Guid CorrespondenceId { get; init; }
        public required DateTime Timestamp { get; init; }
        public required string ActorId { get; init; }
        public required string ActorName { get; init; }
        public required int Status { get; init; }
        public required string ActivityType { get; init; }
    }

    public record ExportProgress
    {
        public long TotalProcessed { get; init; }
        public long TotalCount { get; init; }
        public int BatchNumber { get; init; }
        public TimeSpan ElapsedTime { get; init; }
        public double PercentComplete => TotalCount > 0 ? (TotalProcessed / (double)TotalCount) * 100 : 0;
        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                if (TotalProcessed == 0 || ElapsedTime.TotalSeconds < 1) return TimeSpan.Zero;
                var rate = TotalProcessed / ElapsedTime.TotalSeconds;
                var remaining = TotalCount - TotalProcessed;
                return TimeSpan.FromSeconds(remaining / rate);
            }
        }
    }
}
