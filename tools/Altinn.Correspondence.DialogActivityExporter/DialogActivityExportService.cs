using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Altinn.Correspondence.DialogActivityExporter;

public class DialogActivityExportService
{
    private readonly string _connectionString;
    private readonly int _batchSize;
    private readonly ILogger<DialogActivityExportService> _logger;

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
        DateTime? oldestCorrespondenceDate,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalProcessed = 0L;
        var batchNumber = 0;
        Guid? lastCorrespondenceId = null;

        _logger.LogInformation("Starting export for Issue #{Issue} to {FilePath}", issueNumber, outputFilePath);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get total count
        var totalCount = await GetTotalCountAsync(
            connection, issueNumber, cutoffTimestamp, oldestCorrespondenceDate, cancellationToken);

        _logger.LogInformation("Total records to export: {Count:N0}", totalCount);

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

            var (batchCount, lastId) = await ProcessBatchAsync(
                connection,
                writer,
                issueNumber,
                cutoffTimestamp,
                oldestCorrespondenceDate,
                lastCorrespondenceId,
                cancellationToken);

            if (batchCount == 0)
                break;

            totalProcessed += batchCount;
            lastCorrespondenceId = lastId;
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
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Export completed. Total: {Total:N0} rows in {Elapsed}",
            totalProcessed, stopwatch.Elapsed);
    }

    public async Task ExportBothToCSVAsync(
        string outputFilePath,
        DateTime cutoffTimestamp,
        DateTime? oldestCorrespondenceDate,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting combined export for both issues to {FilePath}", outputFilePath);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get combined count
        var count1716 = await GetTotalCountAsync(connection, 1716, cutoffTimestamp, null, cancellationToken);
        var count1951 = await GetTotalCountAsync(connection, 1951, cutoffTimestamp, oldestCorrespondenceDate, cancellationToken);
        var totalCount = count1716 + count1951;

        _logger.LogInformation("Total records to export: {Count:N0} (1716: {Count1716:N0}, 1951: {Count1951:N0})", 
            totalCount, count1716, count1951);

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
            connection, writer, 1716, cutoffTimestamp, null, 
            count1716, totalProcessed, totalCount, progress, stopwatch, cancellationToken);
        totalProcessed += processed1716;

        // Export Issue #1951
        _logger.LogInformation("Exporting Issue #1951...");
        var processed1951 = await ExportIssueToWriter(
            connection, writer, 1951, cutoffTimestamp, oldestCorrespondenceDate,
            count1951, totalProcessed, totalCount, progress, stopwatch, cancellationToken);
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
        DateTime? oldestCorrespondenceDate,
        long issueTotal,
        long currentTotal,
        long grandTotal,
        IProgress<ExportProgress>? progress,
        System.Diagnostics.Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var totalProcessed = 0L;
        var batchNumber = 0;
        Guid? lastCorrespondenceId = null;

        while (true)
        {
            batchNumber++;

            var (batchCount, lastId) = await ProcessBatchAsync(
                connection,
                writer,
                issueNumber,
                cutoffTimestamp,
                oldestCorrespondenceDate,
                lastCorrespondenceId,
                cancellationToken);

            if (batchCount == 0)
                break;

            totalProcessed += batchCount;
            lastCorrespondenceId = lastId;

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
        }

        return totalProcessed;
    }

    private async Task<long> GetTotalCountAsync(
        NpgsqlConnection connection,
        int issueNumber,
        DateTime cutoffTimestamp,
        DateTime? oldestCorrespondenceDate,
        CancellationToken cancellationToken)
    {
        var (syncFilter, timestampColumn, createdFilter) = GetFiltersForIssue(issueNumber, oldestCorrespondenceDate);

        var countQuery = $@"
            SELECT COUNT(*)
            FROM correspondence.""CorrespondenceStatuses"" stats
            INNER JOIN correspondence.""Correspondences"" corr 
                ON stats.""CorrespondenceId"" = corr.""Id"" 
                AND corr.""Altinn2CorrespondenceId"" IS NOT NULL 
                AND corr.""IsMigrating"" = FALSE
                AND {syncFilter}
                {createdFilter}
            INNER JOIN correspondence.""A2Parties"" ap 
                ON stats.""PartyUuid"" = ap.""PartyUuid""
                AND corr.""Recipient"" <> ap.""RecipientUrn""
            WHERE stats.""Status"" IN (4, 6)
              AND stats.""{timestampColumn}"" < @cutoffTimestamp";

        await using var cmd = new NpgsqlCommand(countQuery, connection);
        cmd.Parameters.AddWithValue("cutoffTimestamp", cutoffTimestamp);
        if (oldestCorrespondenceDate.HasValue)
            cmd.Parameters.AddWithValue("oldestDate", oldestCorrespondenceDate.Value);
        cmd.CommandTimeout = 120;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private async Task<(int Count, Guid? LastId)> ProcessBatchAsync(
        NpgsqlConnection connection,
        StreamWriter writer,
        int issueNumber,
        DateTime cutoffTimestamp,
        DateTime? oldestCorrespondenceDate,
        Guid? lastCorrespondenceId,
        CancellationToken cancellationToken)
    {
        var (syncFilter, timestampColumn, createdFilter) = GetFiltersForIssue(issueNumber, oldestCorrespondenceDate);

        var batchQuery = $@"
            SELECT 
                er.""ReferenceValue"" AS DialogId,
                idcFetch.""Id"" AS DialogActivityId,
                stats.""CorrespondenceId"",
                stats.""StatusChanged"" AS Timestamp,
                ap.""OutputActorId"" AS ActorId,
                ap.""Name"" AS ActorName,
                4 AS Status,
                'CorrespondenceOpened' AS ActivityType
            FROM correspondence.""CorrespondenceStatuses"" stats
            INNER JOIN correspondence.""Correspondences"" corr 
                ON stats.""CorrespondenceId"" = corr.""Id"" 
                AND corr.""Altinn2CorrespondenceId"" IS NOT NULL 
                AND corr.""IsMigrating"" = FALSE
                AND {syncFilter}
                {createdFilter}
            INNER JOIN correspondence.""A2Parties"" ap 
                ON stats.""PartyUuid"" = ap.""PartyUuid""
                AND corr.""Recipient"" <> ap.""RecipientUrn""
            INNER JOIN correspondence.""ExternalReferences"" er
                ON stats.""CorrespondenceId"" = er.""CorrespondenceId"" 
                AND er.""ReferenceType"" = 3
            LEFT JOIN correspondence.""IdempotencyKeys"" idcFetch 
                ON stats.""CorrespondenceId"" = idcFetch.""CorrespondenceId"" 
                AND idcFetch.""StatusAction"" = '3'
            WHERE stats.""Status"" = 4
              AND stats.""{timestampColumn}"" < @cutoffTimestamp
              AND (@lastId IS NULL OR stats.""CorrespondenceId"" > @lastId)
            ORDER BY stats.""CorrespondenceId""

            UNION ALL

            SELECT 
                er.""ReferenceValue"" AS DialogId,
                idcConfirm.""Id"" AS DialogActivityId,
                stats.""CorrespondenceId"",
                stats.""StatusChanged"" AS Timestamp,
                ap.""OutputActorId"" AS ActorId,
                ap.""Name"" AS ActorName,
                6 AS Status,
                'CorrespondenceConfirmed' AS ActivityType
            FROM correspondence.""CorrespondenceStatuses"" stats
            INNER JOIN correspondence.""Correspondences"" corr 
                ON stats.""CorrespondenceId"" = corr.""Id"" 
                AND corr.""Altinn2CorrespondenceId"" IS NOT NULL 
                AND corr.""IsMigrating"" = FALSE
                AND {syncFilter}
                {createdFilter}
            INNER JOIN correspondence.""A2Parties"" ap 
                ON stats.""PartyUuid"" = ap.""PartyUuid""
                AND corr.""Recipient"" <> ap.""RecipientUrn""
            INNER JOIN correspondence.""ExternalReferences"" er 
                ON stats.""CorrespondenceId"" = er.""CorrespondenceId"" 
                AND er.""ReferenceType"" = 3
            LEFT JOIN correspondence.""IdempotencyKeys"" idcConfirm
                ON stats.""CorrespondenceId"" = idcConfirm.""CorrespondenceId"" 
                AND idcConfirm.""StatusAction"" = '6'
            WHERE stats.""Status"" = 6
              AND stats.""{timestampColumn}"" < @cutoffTimestamp
              AND (@lastId IS NULL OR stats.""CorrespondenceId"" > @lastId)
            ORDER BY stats.""CorrespondenceId""

            ORDER BY ""CorrespondenceId""
            LIMIT @batchSize";

        await using var cmd = new NpgsqlCommand(batchQuery, connection);
        cmd.Parameters.AddWithValue("cutoffTimestamp", cutoffTimestamp);
        cmd.Parameters.AddWithValue("lastId", (object?)lastCorrespondenceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("batchSize", _batchSize);
        if (oldestCorrespondenceDate.HasValue)
            cmd.Parameters.AddWithValue("oldestDate", oldestCorrespondenceDate.Value);
        cmd.CommandTimeout = 300;

        var count = 0;
        Guid? lastProcessedId = null;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var correspondenceId = reader.GetGuid(2);
            lastProcessedId = correspondenceId;

            var line = FormatCSVLine(
                reader.GetString(0),  // DialogId
                reader.IsDBNull(1) ? "" : reader.GetGuid(1).ToString(),  // DialogActivityId
                reader.GetDateTime(3).ToString("o", CultureInfo.InvariantCulture),  // Timestamp
                reader.GetString(4),  // ActorId
                EscapeCSV(reader.GetString(5)),  // ActorName
                reader.GetString(7)   // ActivityType
            );

            await writer.WriteLineAsync(line);
            count++;
        }

        return (count, lastProcessedId);
    }

    private static (string SyncFilter, string TimestampColumn, string CreatedFilter) GetFiltersForIssue(
        int issueNumber,
        DateTime? oldestDate)
    {
        return issueNumber switch
        {
            1951 => (
                "stats.\"SyncedFromAltinn2\" IS NULL",
                "StatusChanged",
                oldestDate.HasValue ? "AND corr.\"Created\" > @oldestDate" : ""
            ),
            1716 => (
                "stats.\"SyncedFromAltinn2\" IS NOT NULL",
                "SyncedFromAltinn2",
                ""
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
