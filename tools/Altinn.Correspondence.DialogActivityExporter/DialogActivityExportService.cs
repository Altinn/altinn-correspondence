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
        bool freshStart = false,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var checkpointPath = outputFilePath + ".checkpoint";
        var totalProcessed = 0L;
        var batchNumber = 0;
        (Guid correspondenceId, int status)? lastCursor = null;

        // Delete checkpoint if fresh start requested
        if (freshStart && File.Exists(checkpointPath))
        {
            File.Delete(checkpointPath);
            _logger.LogInformation("Fresh start requested - deleted existing checkpoint");
        }

        // Try to resume from checkpoint
        var checkpoint = freshStart ? null : await LoadCheckpointAsync(checkpointPath);
        var isResuming = checkpoint != null 
            && checkpoint.IssueNumber == issueNumber 
            && checkpoint.CutoffTimestamp == cutoffTimestamp
            && File.Exists(outputFilePath);

        if (isResuming && checkpoint != null)
        {
            _logger.LogInformation("Resuming export from checkpoint: {Processed:N0} rows, batch {Batch}", 
                checkpoint.TotalProcessed, checkpoint.BatchNumber);
            totalProcessed = checkpoint.TotalProcessed;
            batchNumber = checkpoint.BatchNumber;
            if (checkpoint.LastCorrespondenceId.HasValue && checkpoint.LastStatus.HasValue)
            {
                lastCursor = (checkpoint.LastCorrespondenceId.Value, checkpoint.LastStatus.Value);
            }
        }

        // Set test mode for query logging
        _isTestMode = maxBatches.HasValue;

        _logger.LogInformation("{Mode} export for Issue #{Issue} to {FilePath}", 
            isResuming ? "Resuming" : "Starting", issueNumber, outputFilePath);
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

        // Open file in append mode if resuming, otherwise create new
        var fileMode = isResuming ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(outputFilePath, fileMode, FileAccess.Write, FileShare.None, bufferSize: 65536);
        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, bufferSize: 65536);

        // Write CSV header only if creating new file
        if (!isResuming)
        {
            await writer.WriteLineAsync("DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var checkpointInterval = 10; // Save checkpoint every N batches

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

            // Save checkpoint periodically
            if (batchNumber % checkpointInterval == 0)
            {
                await SaveCheckpointAsync(checkpointPath, new ExportCheckpoint
                {
                    IssueNumber = issueNumber,
                    CutoffTimestamp = cutoffTimestamp,
                    TotalProcessed = totalProcessed,
                    BatchNumber = batchNumber,
                    LastCorrespondenceId = lastCursor?.correspondenceId,
                    LastStatus = lastCursor?.status,
                    CheckpointTime = DateTime.UtcNow
                });
                _logger.LogDebug("Checkpoint saved at batch {BatchNumber}", batchNumber);
            }

            if (batchCount < _batchSize)
                break;

            // Check if we've reached the max batch limit (test mode)
            if (maxBatches.HasValue && batchNumber >= maxBatches.Value)
            {
                _logger.LogInformation("Reached max batch limit ({MaxBatches}). Stopping test export.", maxBatches.Value);
                // Save final checkpoint for test mode
                await SaveCheckpointAsync(checkpointPath, new ExportCheckpoint
                {
                    IssueNumber = issueNumber,
                    CutoffTimestamp = cutoffTimestamp,
                    TotalProcessed = totalProcessed,
                    BatchNumber = batchNumber,
                    LastCorrespondenceId = lastCursor?.correspondenceId,
                    LastStatus = lastCursor?.status,
                    CheckpointTime = DateTime.UtcNow
                });
                break;
            }
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Export completed. Total: {Total:N0} rows in {Elapsed}",
            totalProcessed, stopwatch.Elapsed);

        // Delete checkpoint file on successful completion
        if (File.Exists(checkpointPath))
        {
            File.Delete(checkpointPath);
            _logger.LogInformation("Checkpoint file deleted after successful completion");
        }
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
        string countQuery;

        if (issueNumber == 1716)
        {
            // Use helper table for Issue #1716 - much faster count
            countQuery = @"
                SELECT COUNT(*)
                FROM correspondence.""A2Iss1716A2Events""
                WHERE ""Status"" IN (4, 6)";
        }
        else
        {
            // Standard count for Issue #1951
            var (syncFilter, timestampFilter) = GetFiltersForIssue(issueNumber);

            countQuery = $@"
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
        }

        await using var cmd = new NpgsqlCommand(countQuery, connection);
        if (issueNumber == 1951)
        {
            cmd.Parameters.AddWithValue("cutoffTimestamp", cutoffTimestamp);
        }
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

        var batchTimer = System.Diagnostics.Stopwatch.StartNew();

        var fetchTimer = System.Diagnostics.Stopwatch.StartNew();
        var status4Results = await FetchStatusRecordsAsync(
            connection, issueNumber, cutoffTimestamp,
            statusValue: 4, lastCursor, cancellationToken);

        var status6Results = await FetchStatusRecordsAsync(
            connection, issueNumber, cutoffTimestamp,
            statusValue: 6, lastCursor, cancellationToken);
        var fetchTime = fetchTimer.ElapsedMilliseconds;

        // Merge and sort in-memory (much faster than database ORDER BY on UNION ALL)
        var mergeTimer = System.Diagnostics.Stopwatch.StartNew();
        var allResults = status4Results
            .Concat(status6Results)
            .OrderBy(r => r.CorrespondenceId)
            .ThenBy(r => r.Status)
            .Take(_batchSize)
            .ToList();
        var mergeTime = mergeTimer.ElapsedMilliseconds;

        // Write to CSV
        var writeTimer = System.Diagnostics.Stopwatch.StartNew();
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
        var writeTime = writeTimer.ElapsedMilliseconds;
        batchTimer.Stop();

        _logger.LogInformation(
            "Batch timing: Fetch={FetchMs}ms, Merge={MergeMs}ms, Write={WriteMs}ms, Total={TotalMs}ms, Rows={RowCount}",
            fetchTime, mergeTime, writeTime, batchTimer.ElapsedMilliseconds, allResults.Count);

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
        var activityType = statusValue == 4 ? "CorrespondenceOpened" : "CorrespondenceConfirmed";
        var idcJoinAlias = statusValue == 4 ? "idcFetch" : "idcConfirm";

        // Map CorrespondenceStatus to StatusAction:
        // Status 4 (Read) -> StatusAction 3 (Fetched)
        // Status 6 (Confirmed) -> StatusAction 6 (Confirmed)
        // NOTE: StatusAction is stored as TEXT in database, so we use string comparison
        var statusActionValue = statusValue == 4 ? "3" : "6";

        // Build sync filter for CTE (moves stats reference into CTE scope)
        var cteSyncFilter = issueNumber == 1716 
            ? "AND stats.\"SyncedFromAltinn2\" IS NOT NULL"
            : "AND stats.\"SyncedFromAltinn2\" IS NULL";

        // Issue #1716: Use helper table for optimized performance
        // Issue #1951: Use standard CTE approach (no helper table yet)
        string query;

        if (issueNumber == 1716)
        {
            // Build cursor predicate for helper table query
            // CRITICAL: Cursor must reference columns in SELECT list for DISTINCT to work
            // Since stats."CorrespondenceId" is in SELECT, we use that for cursor comparison
            // Note: a2Events."CorrespondenceId" = stats."CorrespondenceId" (join condition),
            // but for index usage, we filter on a2Events first, then join
            var a2EventsCursorPredicate = lastCursor.HasValue 
                ? "AND (a2Events.\"CorrespondenceId\", a2Events.\"Status\") > (@lastId, @lastStatus)"
                : "";

            // Optimized query using A2Iss1716A2Events helper table
            // This table pre-filters to only the affected correspondence events from Altinn 2
            // Performance: Much faster than scanning 1.94B rows, uses direct event matching
            query = $@"
                SELECT DISTINCT
                    er.""ReferenceValue"" AS DialogId,
                    {idcJoinAlias}.""Id"" AS DialogActivityId,
                    stats.""CorrespondenceId"",
                    stats.""StatusChanged"" AS Timestamp,
                    ap.""OutputActorId"" AS ActorId,
                    ap.""Name"" AS ActorName,
                    {statusValue} AS Status,
                    '{activityType}' AS ActivityType
                FROM correspondence.""A2Iss1716A2Events"" a2Events
                INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
                    ON a2Events.""CorrespondenceId"" = stats.""CorrespondenceId"" 
                    AND a2Events.""Status"" = stats.""Status"" 
                    AND a2Events.""PartyUuid"" = stats.""PartyUuid""
                INNER JOIN correspondence.""ExternalReferences"" er
                    ON a2Events.""CorrespondenceId"" = er.""CorrespondenceId""
                    AND er.""ReferenceType"" = 3
                INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
                    ON a2Events.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId""
                    AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
                INNER JOIN correspondence.""A2Parties"" ap 
                    ON stats.""PartyUuid"" = ap.""PartyUuid""
                WHERE a2Events.""Status"" = {statusValue}
                  {a2EventsCursorPredicate}
                ORDER BY stats.""CorrespondenceId"", Status
                LIMIT @fetchLimit";
        }
        else
        {
            // Get filters for Issue #1951
            var (syncFilter, timestampFilter) = GetFiltersForIssue(issueNumber);

            // Build cursor predicate for standard CTE query
            var cursorPredicate = lastCursor.HasValue 
                ? "AND (stats.\"CorrespondenceId\", stats.\"Status\") > (@lastId, @lastStatus)"
                : "";

            // Standard CTE approach for Issue #1951
            // NOTE: CTE filters CorrespondenceStatuses only, keeping it simple for index optimization
            // The subsequent JOINs filter the result set further
            query = $@"
                WITH filtered AS (
                    SELECT 
                        stats.""CorrespondenceId"",
                        stats.""PartyUuid"",
                        stats.""StatusChanged"",
                        stats.""Status""
                    FROM correspondence.""CorrespondenceStatuses"" stats
                    WHERE stats.""Status"" = {statusValue}
                      {timestampFilter}
                      {cteSyncFilter}
                      {cursorPredicate}
                    ORDER BY stats.""CorrespondenceId"", stats.""Status""
                    LIMIT @fetchLimit
                )
                SELECT 
                    er.""ReferenceValue"" AS DialogId,
                    {idcJoinAlias}.""Id"" AS DialogActivityId,
                    filtered.""CorrespondenceId"",
                    filtered.""StatusChanged"" AS Timestamp,
                    ap.""OutputActorId"" AS ActorId,
                    ap.""Name"" AS ActorName,
                    {statusValue} AS Status,
                    '{activityType}' AS ActivityType
                FROM filtered
                INNER JOIN correspondence.""Correspondences"" corr 
                    ON filtered.""CorrespondenceId"" = corr.""Id"" 
                    AND corr.""Altinn2CorrespondenceId"" IS NOT NULL 
                    AND corr.""IsMigrating"" = FALSE
                INNER JOIN correspondence.""A2Parties"" ap 
                    ON filtered.""PartyUuid"" = ap.""PartyUuid""
                    AND corr.""Recipient"" <> ap.""RecipientUrn""
                INNER JOIN correspondence.""ExternalReferences"" er
                    ON filtered.""CorrespondenceId"" = er.""CorrespondenceId"" 
                    AND er.""ReferenceType"" = 3
                INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
                    ON filtered.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId"" 
                    AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
                ORDER BY filtered.""CorrespondenceId"", filtered.""Status""";
        }

        await using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("cutoffTimestamp", cutoffTimestamp);

        // Only add cursor parameters if we have a cursor value
        if (lastCursor.HasValue)
        {
            cmd.Parameters.AddWithValue("lastId", lastCursor.Value.correspondenceId);
            cmd.Parameters.AddWithValue("lastStatus", lastCursor.Value.status);
        }

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

        var queryStartTime = System.Diagnostics.Stopwatch.StartNew();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var queryExecutionTime = queryStartTime.ElapsedMilliseconds;

        var readStartTime = System.Diagnostics.Stopwatch.StartNew();
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
        var readTime = readStartTime.ElapsedMilliseconds;
        queryStartTime.Stop();

        _logger.LogInformation(
            "Status {Status} query: ExecuteReader={ExecuteMs}ms, Read {Count} rows={ReadMs}ms, Total={TotalMs}ms",
            statusValue, queryExecutionTime, results.Count, readTime, queryStartTime.ElapsedMilliseconds);

        return results;
    }

    private static async Task SaveCheckpointAsync(string checkpointPath, ExportCheckpoint checkpoint)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(checkpoint, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(checkpointPath, json);
    }

    private static async Task<ExportCheckpoint?> LoadCheckpointAsync(string checkpointPath)
    {
        if (!File.Exists(checkpointPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(checkpointPath);
            return System.Text.Json.JsonSerializer.Deserialize<ExportCheckpoint>(json);
        }
        catch (Exception)
        {
            // Corrupt checkpoint file, ignore and start fresh
            return null;
        }
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

    // Checkpoint data for resumable exports
    internal record ExportCheckpoint
    {
        public int IssueNumber { get; init; }
        public DateTime CutoffTimestamp { get; init; }
        public long TotalProcessed { get; init; }
        public int BatchNumber { get; init; }
        public Guid? LastCorrespondenceId { get; init; }
        public int? LastStatus { get; init; }
        public DateTime CheckpointTime { get; init; }
    }
}
