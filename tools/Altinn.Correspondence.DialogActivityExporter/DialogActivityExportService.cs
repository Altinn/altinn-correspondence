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
    private readonly int _throttleDelayMs;
    private readonly ILogger<DialogActivityExportService> _logger;
    private bool _isTestMode; // Track test mode for query logging

    public DialogActivityExportService(
        string connectionString,
        int batchSize,
        ILogger<DialogActivityExportService> logger,
        int throttleDelayMs = 1000)
    {
        _connectionString = connectionString;
        _batchSize = batchSize;
        _throttleDelayMs = throttleDelayMs;
        _logger = logger;
    }

    public async Task ExportToCSVAsync(
        string outputFilePath,
        int issueNumber,
        long preCalculatedCount = 0,
        int? maxBatches = null,
        bool freshStart = false,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var checkpointPath = outputFilePath + ".checkpoint.json";
        var totalProcessed = 0L;
        var batchNumber = 0;
        Guid? lastStatus4CorrespondenceId = null;
        Guid? lastStatus6CorrespondenceId = null;
        bool status4Exhausted = false;
        bool status6Exhausted = false;

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
            && File.Exists(outputFilePath);

        if (isResuming && checkpoint != null)
        {
            _logger.LogInformation("Resuming export from checkpoint: {Processed:N0} rows, batch {Batch}", 
                checkpoint.TotalProcessed, checkpoint.BatchNumber);
            totalProcessed = checkpoint.TotalProcessed;
            batchNumber = checkpoint.BatchNumber;
            lastStatus4CorrespondenceId = checkpoint.LastStatus4CorrespondenceId;
            lastStatus6CorrespondenceId = checkpoint.LastStatus6CorrespondenceId;
            status4Exhausted = checkpoint.Status4Exhausted;
            status6Exhausted = checkpoint.Status6Exhausted;
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

        while (true)
        {
            batchNumber++;
            var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var (batchCount, newStatus4Cursor, newStatus6Cursor) = await ProcessBatchAsync(
                connection,
                writer,
                issueNumber,
                lastStatus4CorrespondenceId,
                lastStatus6CorrespondenceId,
                status4Exhausted,
                status6Exhausted,
                batchNumber,
                cancellationToken);

            if (batchCount == 0)
                break;

            totalProcessed += batchCount;

            // Update cursors and check if statuses are exhausted
            bool status4HadResults = newStatus4Cursor != lastStatus4CorrespondenceId;
            bool status6HadResults = newStatus6Cursor != lastStatus6CorrespondenceId;

            lastStatus4CorrespondenceId = newStatus4Cursor;
            lastStatus6CorrespondenceId = newStatus6Cursor;

            // Mark as exhausted if cursor didn't advance (no new results)
            if (!status4Exhausted && !status4HadResults)
            {
                status4Exhausted = true;
                _logger.LogInformation("Status 4 exhausted - will skip in future batches");
            }
            if (!status6Exhausted && !status6HadResults)
            {
                status6Exhausted = true;
                _logger.LogInformation("Status 6 exhausted - will skip in future batches");
            }

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

            // Save checkpoint after every batch (data already flushed to disk in ProcessBatchAsync)
            await SaveCheckpointAsync(checkpointPath, new ExportCheckpoint
            {
                IssueNumber = issueNumber,
                TotalProcessed = totalProcessed,
                BatchNumber = batchNumber,
                LastStatus4CorrespondenceId = lastStatus4CorrespondenceId,
                LastStatus6CorrespondenceId = lastStatus6CorrespondenceId,
                Status4Exhausted = status4Exhausted,
                Status6Exhausted = status6Exhausted,
                CheckpointTime = DateTime.UtcNow
            });
            _logger.LogDebug("Checkpoint saved at batch {BatchNumber}", batchNumber);

            // Add a delay to avoid Azure database or network throttling
            if (_throttleDelayMs > 0)
            {
                await Task.Delay(_throttleDelayMs, cancellationToken);
                _logger.LogDebug("Throttling mitigation: {DelayMs}ms delay after batch {BatchNumber}", _throttleDelayMs, batchNumber);
            }

            // Check if we've reached the max batch limit (test mode)
            if (maxBatches.HasValue && batchNumber >= maxBatches.Value)
            {
                _logger.LogInformation("Reached max batch limit ({MaxBatches}). Stopping test export.", maxBatches.Value);
                // Save final checkpoint for test mode
                await SaveCheckpointAsync(checkpointPath, new ExportCheckpoint
                {
                    IssueNumber = issueNumber,
                    TotalProcessed = totalProcessed,
                    BatchNumber = batchNumber,
                    LastStatus4CorrespondenceId = lastStatus4CorrespondenceId,
                    LastStatus6CorrespondenceId = lastStatus6CorrespondenceId,
                    Status4Exhausted = status4Exhausted,
                    Status6Exhausted = status6Exhausted,
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
        long preCalculatedCount1716 = 0,
        long preCalculatedCount1951 = 0,
        int? maxBatches = null,
        bool freshStart = false,
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
            connection, writer, 1716, 
            count1716, totalProcessed, totalCount, maxBatches, progress, stopwatch, cancellationToken);
        totalProcessed += processed1716;

        // Export Issue #1951
        _logger.LogInformation("Exporting Issue #1951...");
        var processed1951 = await ExportIssueToWriter(
            connection, writer, 1951,
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
        Guid? lastStatus4CorrespondenceId = null;
        Guid? lastStatus6CorrespondenceId = null;
        bool status4Exhausted = false;
        bool status6Exhausted = false;

        while (true)
        {
            batchNumber++;

            var (batchCount, newStatus4Cursor, newStatus6Cursor) = await ProcessBatchAsync(
                connection,
                writer,
                issueNumber,
                lastStatus4CorrespondenceId,
                lastStatus6CorrespondenceId,
                status4Exhausted,
                status6Exhausted,
                batchNumber,
                cancellationToken);

            if (batchCount == 0)
                break;

            totalProcessed += batchCount;

            // Update cursors and check if statuses are exhausted
            bool status4HadResults = newStatus4Cursor != lastStatus4CorrespondenceId;
            bool status6HadResults = newStatus6Cursor != lastStatus6CorrespondenceId;

            lastStatus4CorrespondenceId = newStatus4Cursor;
            lastStatus6CorrespondenceId = newStatus6Cursor;

            // Mark as exhausted if cursor didn't advance (no new results)
            if (!status4Exhausted && !status4HadResults)
            {
                status4Exhausted = true;
                _logger.LogInformation("Issue #{Issue}: Status 4 exhausted", issueNumber);
            }
            if (!status6Exhausted && !status6HadResults)
            {
                status6Exhausted = true;
                _logger.LogInformation("Issue #{Issue}: Status 6 exhausted", issueNumber);
            }

            // Report progress with combined totals
            progress?.Report(new ExportProgress
            {
                TotalProcessed = currentTotal + totalProcessed,
                TotalCount = grandTotal,
                BatchNumber = batchNumber,
                ElapsedTime = stopwatch.Elapsed
            });

            // Check if we've reached the max batch limit (test mode)
            if (maxBatches.HasValue && batchNumber >= maxBatches.Value)
            {
                _logger.LogInformation("Issue #{Issue}: Reached max batch limit ({MaxBatches}). Stopping test export.", issueNumber, maxBatches.Value);
                break;
            }
        }

        return totalProcessed;
    }

    private async Task<(int Count, Guid? LastStatus4CorrespondenceId, Guid? LastStatus6CorrespondenceId)> ProcessBatchAsync(
        NpgsqlConnection connection,
        StreamWriter writer,
        int issueNumber,
        Guid? lastStatus4CorrespondenceId,
        Guid? lastStatus6CorrespondenceId,
        bool status4Exhausted,
        bool status6Exhausted,
        int batchNumber,
        CancellationToken cancellationToken)
    {
        // OPTIMIZATION: Run Status 4 and Status 6 as separate queries with independent cursors
        // This ensures each status type progresses independently, avoiding data loss
        // when one status has significantly more records than the other
        //
        // Fetch Strategy: Fetch up to batchSize from EACH status, process ALL results
        // Example: batchSize=5000 → fetch 5000 from Status 4 + 5000 from Status 6
        // → process all ~10000 rows (no waste!)
        // This doubles throughput per batch and eliminates wasted database work entirely.
        //
        // Skip Exhausted Queries: Once a status returns 0 rows, we skip querying it
        // in subsequent batches (saves ~5ms per batch × thousands of batches)

        var batchTimer = System.Diagnostics.Stopwatch.StartNew();

        var fetchTimer = System.Diagnostics.Stopwatch.StartNew();

        // Only query Status 4 if not exhausted
        var status4Results = status4Exhausted 
            ? new List<DialogActivityRecord>()
            : await FetchStatusRecordsAsync(
                connection, issueNumber,
                statusValue: 4, lastStatus4CorrespondenceId, _batchSize, cancellationToken);

        // Only query Status 6 if not exhausted
        var status6Results = status6Exhausted
            ? new List<DialogActivityRecord>()
            : await FetchStatusRecordsAsync(
                connection, issueNumber,
                statusValue: 6, lastStatus6CorrespondenceId, _batchSize, cancellationToken);

        var fetchTime = fetchTimer.ElapsedMilliseconds;

        // Merge and sort in-memory (much faster than database ORDER BY on UNION ALL)
        // Process ALL fetched results (no Take/limit) - eliminates waste entirely
        var mergeTimer = System.Diagnostics.Stopwatch.StartNew();
        var allResults = status4Results
            .Concat(status6Results)
            .OrderBy(r => r.CorrespondenceId)
            .ThenBy(r => r.Status)
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

        var rowsPerSecond = batchTimer.ElapsedMilliseconds > 0 
            ? (int)(allResults.Count / (batchTimer.ElapsedMilliseconds / 1000.0))
            : 0;

        _logger.LogInformation(
            "Batch #{BatchNum} timing: Fetch={FetchMs}ms, Merge={MergeMs}ms, Write={WriteMs}ms, Total={TotalMs}ms, Rows={RowCount} (S4={S4Count}, S6={S6Count}), Rate={RowsPerSec} rows/sec",
            batchNumber, fetchTime, mergeTime, writeTime, batchTimer.ElapsedMilliseconds, allResults.Count, status4Results.Count, status6Results.Count, rowsPerSecond);

        // Track the last CorrespondenceId for each status independently
        Guid? newStatus4Cursor = lastStatus4CorrespondenceId;
        Guid? newStatus6Cursor = lastStatus6CorrespondenceId;

        if (status4Results.Count > 0)
        {
            newStatus4Cursor = status4Results[^1].CorrespondenceId;
        }
        if (status6Results.Count > 0)
        {
            newStatus6Cursor = status6Results[^1].CorrespondenceId;
        }

        return (allResults.Count, newStatus4Cursor, newStatus6Cursor);
    }

    private async Task<List<DialogActivityRecord>> FetchStatusRecordsAsync(
        NpgsqlConnection connection,
        int issueNumber,
        int statusValue,
        Guid? lastCorrespondenceId,
        int fetchLimit,
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

        var a2HelperTableName = issueNumber == 1716 ? "A2Iss1716A2Events" : "A2Iss1951A2Events";

        // Issue #1716: Use helper table for optimized performance
        // Issue #1951: Use standard CTE approach (no helper table yet)
        string query;

        // Build cursor predicate for helper table query (used inside subquery)
        // Reference column directly without alias since a2Events alias is defined outside subquery
        var a2EventsCursorPredicate = lastCorrespondenceId.HasValue
            ? "AND \"CorrespondenceId\" > @lastId"
            : "";

        // Optimized query using helper tables with limited initial scan
        // These tables pre-filter to only the affected correspondence events from Altinn 2
        // The helper tables (A2Iss1716A2Events, A2Iss1951A2Events) have already filtered for:
        //   - Valid correspondences (Altinn2CorrespondenceId IS NOT NULL, IsMigrating = FALSE)
        //   - Correct recipient/actor combinations (Recipient <> RecipientUrn)
        //   - Cleaned up duplicates (helper table maintenance has removed duplicate events)
        //
        // PERFORMANCE CHALLENGE: Status 4 has 190M rows, Status 6 has 846K rows
        // Solution: Use row limiting with proper index usage to avoid sequential scans
        //
        // INDEX OPTIMIZATION: Query must use index IX_A2Iss1951A2Events_Status_CorrId or similar
        // Key: Filter on Status first (WHERE Status = X), then order by CorrespondenceId
        // This ensures index scan, not sequential scan on massive Status 4 dataset
        //
        // DISTINCT: Likely redundant now that helper table duplicates have been cleaned up
        // and IdempotencyKeys don't have multiple entries per CorrespondenceId+StatusAction.
        // Kept for safety but could be removed after verification.
        //
        // TIMESTAMP MATCHING: Ensures precise matching between a2Events and CorrespondenceStatuses
        // We filter by matching a2Events.Timestamp = stats.StatusChanged to export the exact
        // event that was synced.

        // Use 1:1 ratio: scan exactly fetchLimit rows from helper table
        // Simple and efficient - some batches may return slightly fewer rows due to join failures
        // Status 4: ~99.86% efficient (6 out of 5000 don't match joins)
        // Status 6: ~95% efficient (255 out of 5010 don't match joins)
        // Result: Status 6 needs ~5% more batches, but each is still fast (~73ms)
        var helperTableScanLimit = fetchLimit;

        // Use subquery with LIMIT to force early row limiting while preserving index usage
        // PostgreSQL can push down the LIMIT and use the index efficiently
        //
        // NO DISTINCT: Helper table has been cleaned of duplicates, and IdempotencyKeys
        // doesn't have multiple entries per CorrespondenceId+StatusAction.
        // Removing DISTINCT improves performance by ~65% (163ms → 57ms in testing).
        query = $@"
                SELECT
                    er.""ReferenceValue"" AS DialogId,
                    {idcJoinAlias}.""Id"" AS DialogActivityId,
                    stats.""CorrespondenceId"",
                    stats.""StatusChanged"" AS Timestamp,
                    ap.""OutputActorId"" AS ActorId,
                    ap.""Name"" AS ActorName,
                    {statusValue} AS Status,
                    '{activityType}' AS ActivityType
                FROM (
                    SELECT ""CorrespondenceId"", ""Status"", ""PartyUuid"", ""Timestamp""
                    FROM correspondence.""{a2HelperTableName}""
                    WHERE ""Status"" = {statusValue}
                      {a2EventsCursorPredicate}
                    ORDER BY ""CorrespondenceId""
                    LIMIT @helperTableScanLimit
                ) a2Events
                INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
                    ON a2Events.""CorrespondenceId"" = stats.""CorrespondenceId"" 
                    AND a2Events.""Status"" = stats.""Status"" 
                    AND a2Events.""PartyUuid"" = stats.""PartyUuid""
                    AND a2Events.""Timestamp"" = stats.""StatusChanged""
                INNER JOIN correspondence.""ExternalReferences"" er
                    ON a2Events.""CorrespondenceId"" = er.""CorrespondenceId""
                    AND er.""ReferenceType"" = 3
                INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
                    ON a2Events.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId""
                    AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
                INNER JOIN correspondence.""A2Parties"" ap 
                    ON stats.""PartyUuid"" = ap.""PartyUuid""
                ORDER BY stats.""CorrespondenceId"", {statusValue}
                LIMIT @fetchLimit";

        await using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("helperTableScanLimit", helperTableScanLimit);
        cmd.Parameters.AddWithValue("fetchLimit", fetchLimit);
        if (lastCorrespondenceId.HasValue)
        {
            cmd.Parameters.AddWithValue("lastId", lastCorrespondenceId.Value);
        }
        cmd.CommandTimeout = 300;

        // Log query in test mode for verification
        if (_isTestMode)
        {
            var logQuery = query
                .Replace("@lastId", lastCorrespondenceId.HasValue ? $"'{lastCorrespondenceId.Value}'" : "NULL")
                .Replace("@helperTableScanLimit", helperTableScanLimit.ToString())
                .Replace("@fetchLimit", fetchLimit.ToString());

            _logger.LogInformation("TEST MODE - Executing query for Status {StatusValue} (fetchLimit={FetchLimit}, scanLimit={ScanLimit}):", 
                statusValue, fetchLimit, helperTableScanLimit);
            _logger.LogInformation("{Query}", logQuery);
            _logger.LogInformation("IMPORTANT: Verify EXPLAIN plan uses index scan (IX_A2Iss1951A2Events_Status_CorrId or similar), NOT sequential scan");
        }

        var results = new List<DialogActivityRecord>(fetchLimit);

        var queryStartTime = System.Diagnostics.Stopwatch.StartNew();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var queryExecutionTime = queryStartTime.ElapsedMilliseconds;

        var readStartTime = System.Diagnostics.Stopwatch.StartNew();
        var rowCount = 0;
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

            rowCount++;
            // Early termination: Stop reading once we have fetchLimit rows
            // This avoids processing excess rows from the database
            if (rowCount >= fetchLimit)
            {
                break;
            }
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
                if (rate <= 0) return TimeSpan.Zero;
                var remaining = Math.Max(0, TotalCount - TotalProcessed);
                return TimeSpan.FromSeconds(remaining / rate);
            }
        }
    }

    // Checkpoint data for resumable exports
    internal record ExportCheckpoint
    {
        public int IssueNumber { get; init; }
        public long TotalProcessed { get; init; }
        public int BatchNumber { get; init; }
        public Guid? LastStatus4CorrespondenceId { get; init; }
        public Guid? LastStatus6CorrespondenceId { get; init; }
        public bool Status4Exhausted { get; init; }
        public bool Status6Exhausted { get; init; }
        public DateTime CheckpointTime { get; init; }
    }
}
