// See https://aka.ms/new-console-template for more information
using Altinn.Correspondence.LoadTests.DatabasePopulater;
using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
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
                if (int.TryParse(Console.ReadLine(), out int bulkCopycount))
                {
                    var startTime = DateTime.Now;
                    var options = new BatchingOptions
                    {
                        BatchSize = 10000,
                        Logger = msg => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}"),
                        MaxDegreeOfParallelism = 32
                    };
                    options.Logger($"Starting population of database with batch size of {options.BatchSize} and with {options.MaxDegreeOfParallelism} parallel threads");
                    var databasePopulator = new DatabasePopulator(dbContext.Database.GetConnectionString(), options); 
                    // Generate correspondences and get their IDs
                    var correspondenceIds = databasePopulator.PopulateWithCorrespondences(bulkCopycount);

                    // Generate related records
                    databasePopulator.PopulateWithCorrespondenceStatuses(correspondenceIds);
                    databasePopulator.PopulateWithCorrespondenceContents(correspondenceIds);
                    databasePopulator.PopulateWithCorrespondenceReplyOptions(correspondenceIds);
                    databasePopulator.PopulateWithCorrespondenceNotifications(correspondenceIds);
                    options.Logger($"Finished populating database with {bulkCopycount} correspondences and related rows in {(DateTime.Now - startTime).TotalSeconds} seconds");
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

    private static string EscapeCsv(string value)
    {
        if (value == null || value.ToUpper() == "NULL")
            return "";

        return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
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
