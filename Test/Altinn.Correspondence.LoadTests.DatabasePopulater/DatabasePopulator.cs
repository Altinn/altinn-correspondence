using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Concurrent;
using System.Text;

namespace Altinn.Correspondence.LoadTests.DatabasePopulater;
public class DatabasePopulator
{
    public class BatchingOptions
    {
        public int BatchSize { get; set; } = 50000;
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public Action<string> Logger { get; set; } = Console.WriteLine;
    }
    private class BatchInfo
    {
        public int StartIndex { get; set; }
        public int Size { get; set; }
    }
    public static List<string> PopulateWithCorrespondences(
        ApplicationDbContext appContext,
        int totalCount,
        BatchingOptions options)
    {
        var startTime = DateTime.Now;
        var allIds = new ConcurrentBag<string>();
        (var ssnList, var orgList) = GetPartyList(appContext);
        var ssnCount = ssnList.Count;
        var orgCount = orgList.Count;

        var batches = new List<BatchInfo>();
        var remainingCount = totalCount;
        while (remainingCount > 0)
        {
            var batchSize = Math.Min(options.BatchSize, remainingCount);
            batches.Add(new BatchInfo
            {
                StartIndex = totalCount - remainingCount,
                Size = batchSize
            });
            remainingCount -= batchSize;
        }

        var processedCount = 0;
        var syncLock = new object();

        // Process batches in parallel
        Parallel.ForEach(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
            batch =>
            {
                var random = new Random(Guid.NewGuid().GetHashCode()); // Ensure thread-safe random
                var tempCsvPath = Path.GetTempFileName();

                try
                {
                    // Generate CSV content
                    using (var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
                    {
                        for (int i = 0; i < batch.Size; i++)
                        {
                            var id = Guid.NewGuid().ToString();
                            allIds.Add(id);
                            var senderId = random.Next(orgCount);
                            var recipientId = random.Next(ssnCount + orgCount);
                            var line = CreateCorrespondenceLine(id, random, ssnList, orgList, senderId, recipientId, ssnCount);
                            csvWriter.WriteLine(string.Join(",", line.Select(EscapeCsv)));
                        }
                    }

                    // Import to database
                    using (var connection = new NpgsqlConnection(appContext.Database.GetConnectionString()))
                    {
                        connection.Open();
                        using (var writer = connection.BeginTextImport(@"
                        COPY ""correspondence"".""Correspondences"" (
                            ""Id"", ""ResourceId"", ""Recipient"", ""Sender"", ""SendersReference"", ""MessageSender"", 
                            ""RequestedPublishTime"", ""AllowSystemDeleteAfter"", ""DueDateTime"", ""PropertyList"", 
                            ""IgnoreReservation"", ""Created"", ""Altinn2CorrespondenceId"", ""Published"", ""IsConfirmationNeeded""
                        )
                        FROM STDIN WITH (FORMAT CSV)"))
                        {
                            using var fileReader = new StreamReader(tempCsvPath, Encoding.UTF8);
                            while (!fileReader.EndOfStream)
                            {
                                writer.WriteLine(fileReader.ReadLine());
                            }
                        }
                    }

                    // Update progress
                    lock (syncLock)
                    {
                        processedCount += batch.Size;
                        options.Logger($"Processed {processedCount}/{totalCount} correspondences");
                    }
                }
                finally
                {
                    // Cleanup temp file
                    if (File.Exists(tempCsvPath))
                    {
                        File.Delete(tempCsvPath);
                    }
                }
            });

        options.Logger($"Completed in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
        return allIds.ToList();
    }
    public static async Task PopulateWithCorrespondenceStatusesAsync(
    ApplicationDbContext appContext,
    List<string> correspondenceIds,
    BatchingOptions options)
    {
        var startTime = DateTime.Now;
        var processedCount = 0;
        var syncLock = new object();
        var batches = CreateBatches(correspondenceIds.Count, options.BatchSize);

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
            async (batch, ct) =>
            {
                var tempCsvPath = Path.GetTempFileName();
                try
                {
                    using (var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
                    {
                        for (int j = 0; j < batch.Size; j++)
                        {
                            var correspondenceId = correspondenceIds[batch.StartIndex + j];
                            var initializedline = CreateStatusLine(correspondenceId, CorrespondenceStatus.Initialized);
                            var readyForPublishLine = CreateStatusLine(correspondenceId, CorrespondenceStatus.ReadyForPublish);
                            var publishedLine = CreateStatusLine(correspondenceId, CorrespondenceStatus.Published);
                            csvWriter.WriteLine(string.Join(",", initializedline.Select(EscapeCsv)));
                            csvWriter.WriteLine(string.Join(",", readyForPublishLine.Select(EscapeCsv)));
                            csvWriter.WriteLine(string.Join(",", publishedLine.Select(EscapeCsv)));
                        }
                    }

                    await BulkCopyToTable(
                        appContext,
                        tempCsvPath,
                        "\"correspondence\".\"CorrespondenceStatuses\"",
                        "Id, CorrespondenceId, Status, StatusChanged, StatusText"
                    );

                    lock (syncLock)
                    {
                        processedCount += batch.Size;
                        options.Logger($"Processed statuses for {processedCount}/{correspondenceIds.Count} correspondences");
                    }
                }
                finally
                {
                    if (File.Exists(tempCsvPath))
                    {
                        File.Delete(tempCsvPath);
                    }
                }
            });

        options.Logger($"Completed statuses in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
    }

    public static async Task PopulateWithCorrespondenceContentsAsync(
        ApplicationDbContext appContext,
        List<string> correspondenceIds,
        BatchingOptions options)
    {
        var startTime = DateTime.Now;
        var processedCount = 0;
        var syncLock = new object();
        var batches = CreateBatches(correspondenceIds.Count, options.BatchSize);

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
            async (batch, ct) =>
            {
                var tempCsvPath = Path.GetTempFileName();
                try
                {
                    using (var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
                    {
                        for (int j = 0; j < batch.Size; j++)
                        {
                            var correspondenceId = correspondenceIds[batch.StartIndex + j];
                            var line = CreateContentLine(correspondenceId);
                            csvWriter.WriteLine(string.Join(",", line.Select(EscapeCsv)));
                        }
                    }

                    await BulkCopyToTable(
                        appContext,
                        tempCsvPath,
                        "\"correspondence\".\"CorrespondenceContents\"",
                        "Id,Language,MessageTitle,MessageSummary,MessageBody,CorrespondenceId"
                    );

                    lock (syncLock)
                    {
                        processedCount += batch.Size;
                        options.Logger($"Processed contents for {processedCount}/{correspondenceIds.Count} correspondences");
                    }
                }
                finally
                {
                    if (File.Exists(tempCsvPath))
                    {
                        File.Delete(tempCsvPath);
                    }
                }
            });

        options.Logger($"Completed contents in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
    }

    public static async Task PopulateWithCorrespondenceReplyOptionsAsync(
        ApplicationDbContext appContext,
        List<string> correspondenceIds,
        BatchingOptions options)
    {
        var startTime = DateTime.Now;
        var processedCount = 0;
        var syncLock = new object();
        var batches = CreateBatches(correspondenceIds.Count, options.BatchSize);

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
            async (batch, ct) =>
            {
                var random = new Random(Guid.NewGuid().GetHashCode()); // Thread-safe random
                var tempCsvPath = Path.GetTempFileName();
                try
                {
                    using (var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
                    {
                        for (int j = 0; j < batch.Size; j++)
                        {
                            var correspondenceId = correspondenceIds[batch.StartIndex + j];
                            var replyOptionCount = random.Next(1, 4);
                            for (int k = 0; k < replyOptionCount; k++)
                            {
                                var line = CreateReplyOptionLine(correspondenceId, random, replyOptionCount);
                                csvWriter.WriteLine(string.Join(",", line.Select(EscapeCsv)));
                            }
                        }
                    }

                    await BulkCopyToTable(
                        appContext,
                        tempCsvPath,
                        "\"correspondence\".\"CorrespondenceReplyOptions\"",
                        "Id,LinkURL,LinkText,CorrespondenceId"
                    );

                    lock (syncLock)
                    {
                        processedCount += batch.Size;
                        options.Logger($"Processed reply options for {processedCount}/{correspondenceIds.Count} correspondences");
                    }
                }
                finally
                {
                    if (File.Exists(tempCsvPath))
                    {
                        File.Delete(tempCsvPath);
                    }
                }
            });

        options.Logger($"Completed reply options in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
    }

    public static async Task PopulateWithCorrespondenceNotificationsAsync(
        ApplicationDbContext appContext,
        List<string> correspondenceIds,
        BatchingOptions options)
    {
        var startTime = DateTime.Now;
        var processedCount = 0;
        var syncLock = new object();
        var batches = CreateBatches(correspondenceIds.Count, options.BatchSize);

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
            async (batch, ct) =>
            {
                var random = new Random(Guid.NewGuid().GetHashCode()); // Thread-safe random
                var tempCsvPath = Path.GetTempFileName();
                try
                {
                    using (var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
                    {
                        for (int j = 0; j < batch.Size; j++)
                        {
                            var correspondenceId = correspondenceIds[batch.StartIndex + j];
                            var notificationCount = random.Next(1, 3);
                            for (int k = 0; k < notificationCount; k++)
                            {
                                var line = CreateNotificationLine(correspondenceId, random, k);
                                csvWriter.WriteLine(string.Join(",", line.Select(EscapeCsv)));
                            }
                        }
                    }

                    await BulkCopyToTable(
                        appContext,
                        tempCsvPath,
                        "\"correspondence\".\"CorrespondenceNotifications\"",
                        "Id,NotificationTemplate,NotificationAddress,RequestedSendTime,CorrespondenceId,Created,NotificationChannel,NotificationOrderId,NotificationSent,IsReminder,Altinn2NotificationId"
                    );

                    lock (syncLock)
                    {
                        processedCount += batch.Size;
                        options.Logger($"Processed notifications for {processedCount}/{correspondenceIds.Count} correspondences");
                    }
                }
                finally
                {
                    if (File.Exists(tempCsvPath))
                    {
                        File.Delete(tempCsvPath);
                    }
                }
            });

        options.Logger($"Completed notifications in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
    }



    private static async Task BulkCopyToTable(
        ApplicationDbContext appContext,
        string csvPath,
        string tableName,
        string columns)
    {
        await using var connection = new NpgsqlConnection(appContext.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var writer = connection.BeginTextImport($@"
            COPY {tableName} (
                {string.Join(", ", columns.Split(',').Select(c => $"\"{c.Trim()}\""))}
            )
            FROM STDIN WITH (FORMAT CSV)");

        using var fileReader = new StreamReader(csvPath, Encoding.UTF8);
        while (!fileReader.EndOfStream)
        {
            try
            {
                string? line = await fileReader.ReadLineAsync();
                if (line != null)
                {
                    await writer.WriteAsync(line + "\n");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception when inserting to table {0}: {1}", tableName, e.Message);
            }
        }
    }


    private static (List<string> ssnList, List<string> orgList) GetPartyList(ApplicationDbContext appContext)
    {
        Console.WriteLine("Retrieving list of parties");
        var ssnList = new List<string>();
        var orgList = new List<string>();
        using (var connection = (NpgsqlConnection)appContext.Database.GetDbConnection())
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT fnumber_ak, orgnumber_ak
                FROM correspondence.altinn2party
                WHERE fnumber_ak IS NOT NULL OR orgnumber_ak IS NOT NULL";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                        ssnList.Add(reader.GetString(0));
                    if (!reader.IsDBNull(1))
                        orgList.Add(reader.GetString(1));
                }
            }
        }

        Console.WriteLine("Retrieved list of parties: {0} ssn and {1} orgs", ssnList.Count, orgList.Count);
        return (ssnList, orgList);
    }

    private static string[] CreateCorrespondenceLine(
        string id, Random random, List<string> ssnList, List<string> orgList,
        int senderId, int recipientId, int ssnCount)
    {
        return new[]
        {
            id,
            $"dagl-correspondence-{random.Next(11)}",
            recipientId < ssnCount
                ? $"urn:altinn:person:identifier-no:{ssnList[recipientId]}"
                : $"urn:altinn:organization:identifier-no:{orgList[recipientId - ssnCount]}",
            $"urn:altinn:organization:identifier-no:{orgList[senderId]}",
            " ",
            "",
            DateTimeOffset.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            DateTimeOffset.Now.AddMonths(12).ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            DateTimeOffset.Now.AddMonths(6).ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            " ",
            "false",
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            "",
            DateTimeOffset.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            "false"
        };
    }

    private static string[] CreateStatusLine(string correspondenceId, CorrespondenceStatus status)
    {
        return new[]
        {
            Guid.NewGuid().ToString(),
            correspondenceId,
            ((int)status).ToString(),
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            status.ToString()
        };
    }

    private static string[] CreateContentLine(string correspondenceId)
    {
        return new[]
        {
            Guid.NewGuid().ToString(),                  // Id
            "nb",                                       // Language
            "Meldingstittel",                          // MessageTitle
            "Ett sammendrag for meldingen",            // MessageSummary
            "Dette er tekst i meldingen",              // MessageBody
            correspondenceId                            // CorrespondenceId
        };
    }

    private static string[] CreateReplyOptionLine(string correspondenceId, Random random, int optNum)
    {
        return new[]
        {
            Guid.NewGuid().ToString(),                 // Id
            optNum == 1 ? "test.no" : "www.test.no",   // LinkURL
            "test",                                    // LinkText
            correspondenceId                           // CorrespondenceId
        };
    }

    private static string[] CreateNotificationLine(string correspondenceId, Random random, int notifNum)
    {
        return new[]
        {
            Guid.NewGuid().ToString(),                                                    // Id
            "0",                                                                         // NotificationTemplate
            "",                                                                          // NotificationAddress
            DateTimeOffset.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),     // RequestedSendTime
            correspondenceId,                                                            // CorrespondenceId
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),                 // Created
            "3",                                                                         // NotificationChannel
            Guid.NewGuid().ToString(),                                                  // NotificationOrderId
            "",                                                                          // NotificationSent
            (notifNum == 2).ToString().ToLower(),                                       // IsReminder
            ""                                                                           // Altinn2NotificationId
        };
    }

    private static string EscapeCsv(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
        {
            return $"\"{input.Replace("\"", "\"\"")}\"";
        }
        return input;
    }
    private static IEnumerable<BatchInfo> CreateBatches(int totalCount, int batchSize)
    {
        var batches = new List<BatchInfo>();
        var remainingCount = totalCount;
        var currentIndex = 0;

        while (remainingCount > 0)
        {
            var size = Math.Min(batchSize, remainingCount);
            batches.Add(new BatchInfo
            {
                StartIndex = currentIndex,
                Size = size
            });
            remainingCount -= size;
            currentIndex += size;
        }

        return batches;
    }
}
