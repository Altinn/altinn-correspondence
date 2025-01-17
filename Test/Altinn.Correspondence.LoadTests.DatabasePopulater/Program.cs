// See https://aka.ms/new-console-template for more information
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.LoadTests.DatabasePopulater;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Migrations;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<PostgresSettings>(context.Configuration);
                services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
                {
                    var postgresSettings = serviceProvider.GetRequiredService<IOptions<PostgresSettings>>().Value;
                    var connectionString = new NpgsqlConnectionStringBuilder(postgresSettings.PostgresConnectionString);
                    connectionString.CommandTimeout = 0;
                    options.UseNpgsql(connectionString.ConnectionString);
                });
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!dbContext.Database.CanConnect())
        {
            Console.WriteLine("Database not available. Is connection string correct?");
            return;
        }
        Console.WriteLine("Choose an option:");
        Console.WriteLine("1. Populate with Party List");
        Console.WriteLine("2. Fill with Test Data");
        Console.WriteLine("3. Fill with Test Data using bulk copy");

        string? choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                Console.WriteLine("Enter the path to the party mappings file:");
                string? path = Console.ReadLine();
                if (!string.IsNullOrEmpty(path))
                {
                    PopulateWithPartyList(path, dbContext);
                }
                else
                {
                    Console.WriteLine("Invalid path provided.");
                }
                break;

            case "2":
                Console.WriteLine("Enter the number of correspondence records to generate:");
                if (int.TryParse(Console.ReadLine(), out int count))
                {
                    await FillWithTestDataAsync(dbContext, count);
                }
                else
                {
                    Console.WriteLine("Invalid number provided.");
                }
                break;

            case "3":
                Console.WriteLine("Enter the number of correspondence records to generate:");
                if (int.TryParse(Console.ReadLine(), out int bulkCopycount))
                {
                    var options = new DatabasePopulator.BatchingOptions
                    {
                        BatchSize = 100000,
                        Logger = msg => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}"),
                        MaxDegreeOfParallelism = 32
                    };

                    // Generate correspondences and get their IDs
                    var correspondenceIds = DatabasePopulator.PopulateWithCorrespondences(dbContext, bulkCopycount, options);

                    // Genereate related records
                    var dependentTasks = new[]
                    {
                        DatabasePopulator.PopulateWithCorrespondenceStatusesAsync(dbContext, correspondenceIds, options),
                        DatabasePopulator.PopulateWithCorrespondenceContentsAsync(dbContext, correspondenceIds, options),
                        DatabasePopulator.PopulateWithCorrespondenceReplyOptionsAsync(dbContext, correspondenceIds, options),
                        DatabasePopulator.PopulateWithCorrespondenceNotificationsAsync(dbContext, correspondenceIds, options)
                    };
                    await Task.WhenAll(dependentTasks);

                    try
                    {
                        await Task.WhenAll(dependentTasks);
                    }
                    catch (Exception)
                    {
                        for (int i = 0; i < dependentTasks.Length; i++)
                        {
                            if (dependentTasks[i].IsFaulted)
                            {
                                options.Logger($"Task {i} failed with error: {dependentTasks[i].Exception?.InnerException?.Message}");
                            }
                        }
                        throw; 
                    }

                }
                else
                {
                    Console.WriteLine("Invalid number provided.");
                }
                break;

            default:
                Console.WriteLine("Invalid choice.");
                break;
        }
    }
    static void PopulateWithPartyList(string path, ApplicationDbContext appContext)
    {
        var startTime = DateTime.Now;
        var tempCsvPath = Path.GetTempFileName(); // Temporary CSV file
        using var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8);
        var fileStream = File.Open(path, FileMode.Open);
        using var streamReader = new StreamReader(fileStream, true);

        // Skip header lines
        var line = streamReader.ReadLine();
        line = streamReader.ReadLine();

        int lineCount = 0;
        int invalids = 0;

        // Regex pattern
        string pattern = @"^(\d+|NULL)\s+(\d+|NULL)\s+(.+?)\s+(\d+|NULL)\s+(\d+|NULL)\s+(\d+|NULL)\s+(\d+|NULL)\s+(.+)$";

        while (!streamReader.EndOfStream)
        {
            var row = streamReader.ReadLine();
            lineCount++;
            Match match = Regex.Match(row, pattern);
            if (lineCount % 10000 == 0)
            {
                Console.WriteLine("Currently processing line {0}", lineCount);
            }

            string[] parts = new string[8];
            if (match.Success && match.Groups.Count == 9)
            {
                // Extract fields
                for (int i = 1; i <= 8; i++)
                {
                    parts[i - 1] = match.Groups[i].Value;
                }
            }
            else
            {
                invalids++;
                Console.WriteLine("Invalid line: " + row);
                continue;
            }
            csvWriter.WriteLine(string.Join(",",
                EscapeCsv(parts[0]),
                EscapeCsv(parts[1]),
                EscapeCsv(parts[2]),
                EscapeCsv(parts[3]),
                EscapeCsv(parts[4]),
                EscapeCsv(parts[5]),
                EscapeCsv(parts[6]),
                EscapeCsv(parts[7])
            ));
        }

        csvWriter.Close();

        Console.WriteLine("Finished writing CSV file. Starting bulk copy...");

        // Use COPY command to load CSV into PostgreSQL
        using (var connection = (NpgsqlConnection)appContext.Database.GetDbConnection())
        {
            connection.Open();
            var dropOldTableIfExists = connection.CreateCommand();
            dropOldTableIfExists.CommandText = @"
                DROP TABLE IF EXISTS correspondence.altinn2party;";
            dropOldTableIfExists.ExecuteNonQuery();

            var createTable = connection.CreateCommand();
            createTable.CommandText = @"
                CREATE TABLE correspondence.altinn2party (
                    partyid_pk VARCHAR(255) PRIMARY KEY, 
                    fnumber_ak VARCHAR(255), 
                    name VARCHAR(255), 
                    reguserid VARCHAR(255), 
                    authuserid VARCHAR(255), 
                    orgnumber_ak VARCHAR(255), 
                    unitid_pk VARCHAR(255), 
                    unitname VARCHAR(255)
                )";
            createTable.ExecuteNonQuery();

            using (var writer = connection.BeginTextImport(@"
                COPY correspondence.altinn2party (
                    partyid_pk, fnumber_ak, name, reguserid, authuserid, orgnumber_ak, unitid_pk, unitname
                )
                FROM STDIN WITH (FORMAT CSV)"
            ))
            {
                using var fileReader = new StreamReader(tempCsvPath, Encoding.UTF8);
                while (!fileReader.EndOfStream)
                {
                    line = fileReader.ReadLine();
                    writer.WriteLine(line);
                }
            }
        }

        // Delete temporary CSV file
        File.Delete(tempCsvPath);

        Console.WriteLine("Bulk copy complete in {0} seconds", (DateTime.Now-startTime).TotalSeconds);
        Console.WriteLine("Disregard {0} invalid lines", invalids);
    }

    static void PopulateWithCorrespondences(ApplicationDbContext appContext, int count)
    {
        var startTime = DateTime.Now;
        var tempCsvPath = Path.GetTempFileName(); // Temporary CSV file

        int lineCount = 0;
        var random = new Random();

        // We need to put in memory the entire contents of the PartyList table to generate the correspondence records
        (var ssnList, var orgList) = GetPartyList(appContext);
        var ssnCount = ssnList.Count;
        var orgCount = orgList.Count;
        var totalCount = ssnCount + orgCount;

        using var csvWriter = new StreamWriter(tempCsvPath, false, Encoding.UTF8);
        for (int i = 0; i < count; i++)
        {
            if (i % 10000 == 0)
            {
                Console.WriteLine("Currently processing correspondence #{0}", lineCount);
            }

            var senderId = random.Next(orgCount);
            var recipientId = random.Next(totalCount);

            var correspondenceLine = new string[15];

            correspondenceLine[0] = Guid.NewGuid().ToString();                                                                  //"Id",
            correspondenceLine[1] = "dagl-correspondence-" + random.Next(10).ToString();                                        //"ResourceId",
            correspondenceLine[2] = recipientId < ssnCount ? "urn:altinn:person:identifier-no:" + ssnList[recipientId] :
                                                             "urn:altinn:organization:identifier-no:" + orgList[recipientId];   //"Recipient",
            correspondenceLine[3] = "urn:altinn:organization:identifier-no:" + orgList[senderId];                                                                                     //"Sender",
            correspondenceLine[4] = "";                                                                                         //"SendersReference",
            correspondenceLine[5] = "";                                                                                         //"MessageSender",
            correspondenceLine[6] = DateTimeOffset.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss.fff zzz");                     //"RequestedPublishTime",
            correspondenceLine[7] = DateTimeOffset.Now.AddMonths(12).ToString("yyyy-MM-dd HH:mm:ss.fff zzz");                   //"AllowSystemDeleteAfter",
            correspondenceLine[8] = DateTimeOffset.Now.AddMonths(6).ToString("yyyy-MM-dd HH:mm:ss.fff zzz");                    //"DueDateTime",
            correspondenceLine[9] = "";                                                                                         //"PropertyList",
            correspondenceLine[10] = "false";                                                                                   //"IgnoreReservation",
            correspondenceLine[11] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");                                //"Created",
            correspondenceLine[12] = "";                                                                                        //"Altinn2CorrespondenceId",
            correspondenceLine[13] = DateTimeOffset.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss.fff zzz");                    //"Published",
            correspondenceLine[14] = "false";                                                                                   //"IsConfirmationNeeded"


            csvWriter.WriteLine(string.Join(",",
                EscapeCsv(correspondenceLine[0]),
                EscapeCsv(correspondenceLine[1]),
                EscapeCsv(correspondenceLine[2]),
                EscapeCsv(correspondenceLine[3]),
                EscapeCsv(correspondenceLine[4]),
                EscapeCsv(correspondenceLine[5]),
                EscapeCsv(correspondenceLine[6]),
                EscapeCsv(correspondenceLine[7]),
                EscapeCsv(correspondenceLine[8]),
                EscapeCsv(correspondenceLine[9]),
                EscapeCsv(correspondenceLine[10]),
                EscapeCsv(correspondenceLine[11]),
                EscapeCsv(correspondenceLine[12]),
                EscapeCsv(correspondenceLine[13]),
                EscapeCsv(correspondenceLine[14])
            ));
        }
        csvWriter.Close();
        Console.WriteLine("Finished writing CSV file of {0} correspondences. Starting bulk copy...");

        using (var connection = (NpgsqlConnection)appContext.Database.GetDbConnection())
        {
            connection.Open();
            using (var writer = connection.BeginTextImport(@"
                COPY correspondence.Correspondences (
                    Id, ResourceId, Recipient, Sender, SendersReference, MessageSender, RequestedPublishTime, AllowSystemDeleteAfter, DueDateTime, PropertyList, IgnoreReservation, Created, Altinn2CorrespondenceId, Published, IsConfirmationNeeded
                )
                FROM STDIN WITH (FORMAT CSV)"
            ))
            {
                using var fileReader = new StreamReader(tempCsvPath, Encoding.UTF8);
                while (!fileReader.EndOfStream)
                {
                    writer.WriteLine(fileReader.ReadLine());
                }
            }
        }
        
        // Delete temporary CSV file
        File.Delete(tempCsvPath);
        Console.WriteLine("Bulk copy complete in {0} seconds", (DateTime.Now - startTime).TotalSeconds);
    }

    private static (List<string> ssnList, List<string> orgList) GetPartyList(ApplicationDbContext appContext)
    {
        var ssnList = new List<string>();
        var orgList = new List<string>();
        using (var connection = (NpgsqlConnection)appContext.Database.GetDbConnection())
        {
            connection.Open();
            var getSsnCommand = connection.CreateCommand();
            getSsnCommand.CommandText = @"
                SELECT fnumber_ak
                FROM correspondence.altinn2party
                WHERE fnumber_ak IS NOT NULL";
            using var ssnReader = getSsnCommand.ExecuteReader();
            while (ssnReader.Read())
            {
                ssnList.Add(ssnReader.GetString(0));
            }
            var getOrgCommand = connection.CreateCommand();
            getOrgCommand.CommandText = @"
                SELECT orgnumber_ak
                FROM correspondence.altinn2party
                WHERE orgnumber_ak IS NOT NULL";
            using var orgReader = getOrgCommand.ExecuteReader();
            while (orgReader.Read())
            {
                orgList.Add(orgReader.GetString(0));
            }
        }
        return (ssnList, orgList);
    }

    private static string EscapeCsv(string value)
    {
        if (value == null || value.ToUpper() == "NULL")
            return "";

        return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    static async Task FillWithTestDataAsync(ApplicationDbContext applicationDbContext, int correspondenceCount)
    {
        applicationDbContext.Database.ExecuteSqlRaw(@"
            DROP FUNCTION IF EXISTS generate_test_data(INT);
            DROP PROCEDURE IF EXISTS populate_test_database(bigint, int);
        ");
        applicationDbContext.Database.ExecuteSqlRaw(ReadFileContent("./generate_test_data_function.sql"));
        applicationDbContext.Database.ExecuteSqlRaw(ReadFileContent("./populate_test_database.sql"));

        var startTimeStamp = DateTime.Now;
        
        var threadCount = GetThreadCount(correspondenceCount);
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(RunPopulateQueryAsync(correspondenceCount / threadCount, applicationDbContext.Database.GetConnectionString()));
        }
        if (correspondenceCount % threadCount != 0)
        {
            tasks.Add(RunPopulateQueryAsync(correspondenceCount % threadCount, applicationDbContext.Database.GetConnectionString()));
        }
        await Task.WhenAll(tasks);
        var endTimeStamp = DateTime.Now;
        var secondsRunTime = (endTimeStamp - startTimeStamp).TotalSeconds;
        Console.WriteLine("Successfully filled database with {0} correspondence records in {1} seconds for a rate of {2} correspondences/second", correspondenceCount, secondsRunTime, correspondenceCount / secondsRunTime);
    }

    static int GetThreadCount(int correspondenceCount)
    {
        if (correspondenceCount <= 10 * 1000)
        {
            return 1;
        }
        return Math.Min(64, correspondenceCount/(10 * 1000));
    }

    private static string ReadFileContent(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var streamReader = new StreamReader(fileStream);
        var fileContent = streamReader.ReadToEnd();
        return fileContent;
    }

    private static async Task RunPopulateQueryAsync(int count, string connectionString)
    {
        using var dbConnection = new NpgsqlConnection(connectionString);
        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();
        using var command = dbConnection.CreateCommand();
        command.CommandText = $"CALL populate_test_database({count});";
        await command.ExecuteNonQueryAsync();
    }
}
