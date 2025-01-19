using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Persistence;
using Npgsql;
using System.Collections.Concurrent;
using System.Text;

namespace Altinn.Correspondence.LoadTests.DatabasePopulater;
public class DatabasePopulator
{
    private readonly BatchingOptions _options;
    private readonly NpgsqlDataSource _npgsqlDataSource;

    public DatabasePopulator(string connectionString, BatchingOptions options)
    {
        var npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        npgsqlDataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = options.MaxDegreeOfParallelism + 10;
        _npgsqlDataSource = npgsqlDataSourceBuilder.Build();
        _options = options;
    }
    private class BatchInfo
    {
        public int StartIndex { get; set; }
        public int Size { get; set; }
    }
    /// <summary>
    /// Generic method to handle batch processing of data and bulk copying to database.
    /// Returns a list of generated IDs when needed (for Correspondences) or empty list otherwise.
    /// </summary>
    private List<string> ProcessInBatches(
        int totalCount,
        Func<Random, int, int, (List<string> lines, List<string> ids)> createLinesAndIds,
        string tableName,
        string columnNames)
    {
        var allIds = new ConcurrentBag<string>();
        var processedCount = 0;
        var syncLock = new object();
        var batches = CreateBatches(totalCount, _options.BatchSize);
        var startTime = DateTime.Now;

        Parallel.ForEach(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism },
            batch =>
            {
                var random = new Random(Guid.NewGuid().GetHashCode());
                var tempCsvPath = Path.GetTempFileName();

                var (lines, ids) = createLinesAndIds(random, batch.StartIndex, batch.Size);
                try
                {
                    // Generate CSV content
                    using (var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
                    {
                        foreach (var line in lines)
                        {
                            csvWriter.WriteLine(line);
                        }
                        foreach (var id in ids)
                        {
                            allIds.Add(id);
                        }
                    }

                    // Import to database
                    BulkCopyToTable(tempCsvPath, tableName, columnNames);

                    // Update progress
                    lock (syncLock)
                    {
                        processedCount += batch.Size;
                        _options.Logger($"Processed {processedCount}/{totalCount} records for {tableName}");
                    }
                }
                catch (Exception e)
                {
                    _options.Logger($"Failed batch for {tableName}: {e.Message}");                    
                    throw;
                }
                finally
                {
                    if (File.Exists(tempCsvPath))
                    {
                        File.Delete(tempCsvPath);
                    }
                }
            });

        _options.Logger($"Completed {tableName} in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
        return allIds.ToList();
    }

    public List<string> PopulateWithCorrespondences(int totalCount)
    {
        (var ssnList, var orgList) = GetPartyList();
        var ssnCount = ssnList.Count;
        var orgCount = orgList.Count;

        return ProcessInBatches(
            totalCount,
            (random, startIndex, batchSize) =>
            {
                var lines = new List<string>(batchSize);
                var ids = new List<string>(batchSize);

                for (int i = 0; i < batchSize; i++)
                {
                    var id = Guid.NewGuid().ToString();
                    ids.Add(id);
                    var senderId = random.Next(orgCount);
                    var recipientId = random.Next(ssnCount + orgCount);
                    var line = CreateCorrespondenceLine(id, random, ssnList, orgList, senderId, recipientId, ssnCount);
                    lines.Add(string.Join(",", line.Select(EscapeCsv)));
                }

                return (lines, ids);
            },
            "\"correspondence\".\"Correspondences\"",
            @"Id, ResourceId, Recipient, Sender, SendersReference, MessageSender,
              RequestedPublishTime, AllowSystemDeleteAfter, DueDateTime, PropertyList,
              IgnoreReservation, Created, Altinn2CorrespondenceId, Published, IsConfirmationNeeded"
        );
    }

    public void PopulateWithCorrespondenceStatuses(List<string> correspondenceIds)
    {
        var statuses = Enum.GetValues<CorrespondenceStatus>().Take(3);

        ProcessInBatches(
            correspondenceIds.Count,
            (random, startIndex, batchSize) =>
            {
                var lines = new List<string>(batchSize * statuses.Count());
                for (int i = 0; i < batchSize; i++)
                {
                    var correspondenceId = correspondenceIds[startIndex + i];
                    foreach (var status in statuses)
                    {
                        var line = CreateStatusLine(correspondenceId, status);
                        lines.Add(string.Join(",", line.Select(EscapeCsv)));
                    }
                }
                return (lines, new List<string>());  // Return empty list for IDs
            },
            "\"correspondence\".\"CorrespondenceStatuses\"",
            "Id, CorrespondenceId, Status, StatusChanged, StatusText"
        );
    }

    public void PopulateWithCorrespondenceContents(List<string> correspondenceIds)
    {
        ProcessInBatches(
            correspondenceIds.Count,
            (random, startIndex, batchSize) =>
            {
                var lines = new List<string>(batchSize);
                for (int i = 0; i < batchSize; i++)
                {
                    var correspondenceId = correspondenceIds[startIndex + i];
                    var line = CreateContentLine(correspondenceId);
                    lines.Add(string.Join(",", line.Select(EscapeCsv)));
                }
                return (lines, new List<string>());  // Return empty list for IDs
            },
            "\"correspondence\".\"CorrespondenceContents\"",
            "Id,Language,MessageTitle,MessageSummary,MessageBody,CorrespondenceId"
        );
    }

    public void PopulateWithCorrespondenceReplyOptions(List<string> correspondenceIds)
    {
        ProcessInBatches(
            correspondenceIds.Count,
            (random, startIndex, batchSize) =>
            {
                var lines = new List<string>(batchSize);
                for (int i = 0; i < batchSize; i++)
                {
                    var correspondenceId = correspondenceIds[startIndex + i];
                    var line = CreateReplyOptionLine(correspondenceId);
                    lines.Add(string.Join(",", line.Select(EscapeCsv)));
                }
                return (lines, new List<string>());  // Return empty list for IDs
            },
            "\"correspondence\".\"CorrespondenceReplyOptions\"",
            "Id,LinkURL,LinkText,CorrespondenceId"
        );
    }

    public void PopulateWithCorrespondenceNotifications(List<string> correspondenceIds)
    {
        ProcessInBatches(
            correspondenceIds.Count,
            (random, startIndex, batchSize) =>
            {
                var lines = new List<string>();
                for (int i = 0; i < batchSize; i++)
                {
                    var correspondenceId = correspondenceIds[startIndex + i];
                    var line = CreateNotificationLine(correspondenceId, random, 1);
                    lines.Add(string.Join(",", line.Select(EscapeCsv)));
                }
                return (lines, new List<string>());  // Return empty list for IDs
            },
            "\"correspondence\".\"CorrespondenceNotifications\"",
            "Id,NotificationTemplate,NotificationAddress,RequestedSendTime,CorrespondenceId,Created,NotificationChannel,NotificationOrderId,NotificationSent,IsReminder,Altinn2NotificationId"
        );
    }



    private void BulkCopyToTable(
        string csvPath,
        string tableName,
        string columns)
    {
        DateTime startTimeBulkCopy = DateTime.Now;
        try
        {
            using var connection = _npgsqlDataSource.OpenConnection();
            using var writer = connection.BeginTextImport($@"
                        COPY {tableName} (
                            {string.Join(", ", columns.Split(',').Select(c => $"\"{c.Trim()}\""))}
                        )
                        FROM STDIN WITH (FORMAT CSV)");

            using var fileReader = new StreamReader(csvPath, Encoding.UTF8);
            while (!fileReader.EndOfStream)
            {
                writer.WriteLine(fileReader.ReadLine());
            }

        }
        catch (Exception e)
        {
            _options.Logger("Failed bulk copy: " + e.Message);
            return;
        }

        _options.Logger($"Bulk copied to table {tableName} in {(DateTime.Now - startTimeBulkCopy).TotalSeconds:F1} seconds");
    }


    private (List<string> ssnList, List<string> orgList) GetPartyList()
    {
        Console.WriteLine("Retrieving list of parties");
        var startTime = DateTime.Now;
        var ssnList = new List<string>();
        var orgList = new List<string>();
        using var command = _npgsqlDataSource.CreateCommand();
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

        Console.WriteLine("Retrieved list of parties: {0} ssn and {1} orgs in {2} seconds", ssnList.Count, orgList.Count, (DateTime.Now - startTime).TotalSeconds);
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

    private static string[] CreateReplyOptionLine(string correspondenceId)
    {
        return new[]
        {
            Guid.NewGuid().ToString(),                 // Id
            "www.test.no",                              // LinkURL
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
