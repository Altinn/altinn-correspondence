// See https://aka.ms/new-console-template for more information
using Altinn.Correspondence.LoadTests.DatabasePopulater;
using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;
using System.Text;

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

        Console.WriteLine("Choose an option:");
        Console.WriteLine("1. Populate with Party List");
        Console.WriteLine("2. Fill with Test Data");

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
        Console.WriteLine(line);
        line = streamReader.ReadLine();
        Console.WriteLine(line);

        // Drop old table if exists
        var dropOldTableIfExists = appContext.Database.ExecuteSqlRaw(@"
        DROP TABLE IF EXISTS correspondence.altinn2party;"
        );

        // Create table
        var createResult = appContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE correspondence.altinn2party (
            partyid_pk VARCHAR(255) PRIMARY KEY, 
            name VARCHAR(255), 
            reguserid VARCHAR(255), 
            authuserid VARCHAR(255), 
            orgnumber_ak VARCHAR(255), 
            unitid_pk VARCHAR(255), 
            unitname VARCHAR(255)
        )"
        );

        int lineCount = 0;

        while (!streamReader.EndOfStream)
        {
            var row = streamReader.ReadLine();
            lineCount++;
            var newRow = "";
            var oldRow = "";
            // Remove extra spaces
            do
            {
                oldRow = row;
                newRow = row.Replace("  ", " ");
                row = newRow;
            } while (newRow.Length < oldRow.Length);

            var parts = row.Split(' ');
            if (lineCount % 10000 == 0)
            {
                Console.WriteLine("Currently processing line {0}", lineCount);
            }

            if (parts.Length != 8 || parts[5] == "NULL")
                continue; // Skip invalid rows

            // Write to CSV
            csvWriter.WriteLine(string.Join(",",
                EscapeCsv(parts[0]),
                EscapeCsv(parts[1]),
                EscapeCsv(parts[2]),
                EscapeCsv(parts[3]),
                EscapeCsv(parts[5]),
                EscapeCsv(parts[6]),
                EscapeCsv(parts[7])
            ));
        }

        csvWriter.Close(); // Close the CSV writer

        Console.WriteLine("Finished writing CSV file. Starting bulk copy...");

        // Use COPY command to load CSV into PostgreSQL
        using (var connection = (NpgsqlConnection)appContext.Database.GetDbConnection())
        {
            connection.Open();
            using (var writer = connection.BeginTextImport(@"
        COPY correspondence.altinn2party (
            partyid_pk, name, reguserid, authuserid, orgnumber_ak, unitid_pk, unitname
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
    }

    // Helper function to escape CSV values
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
        // Check if the functions already exist and drop them if they do
        applicationDbContext.Database.ExecuteSqlRaw(@"
            DROP FUNCTION IF EXISTS generate_test_data(INT);
            DROP FUNCTION IF EXISTS populate_test_database(bigint, int);
        ");
        // Create the populate_test_database function
        applicationDbContext.Database.ExecuteSqlRaw(ReadFileContent("./generate_test_data_function.sql"));

        // Create the populate_test_database function
        applicationDbContext.Database.ExecuteSqlRaw(ReadFileContent("./populate_test_database.sql"));

        var startTimeStamp = DateTime.Now;

        var threadCount = 128;
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            var freshConnection = new NpgsqlConnection(applicationDbContext.Database.GetConnectionString());
            tasks.Add(RunPopulateQueryAsync(correspondenceCount, freshConnection));
        }

        await Task.WhenAll(tasks);
        var endTimeStamp = DateTime.Now;
        var secondsRunTime = (endTimeStamp - startTimeStamp).TotalSeconds;
        Console.WriteLine("Successfully filled database with {0} correspondence records in {1} seconds for a rate of {2} correspondences/second", correspondenceCount * threadCount, secondsRunTime, correspondenceCount * threadCount / secondsRunTime);

    }

    private static string ReadFileContent(string path)
    {
        var fileStream = File.OpenRead(path);
        var streamReader = new StreamReader(fileStream);
        var fileContent = streamReader.ReadToEnd();
        return fileContent;
    }

    private static async Task RunPopulateQueryAsync(int count, DbConnection dbConnection)
    {
        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();
        var command = dbConnection.CreateCommand();
        command.CommandText = $"SELECT populate_test_database({count});";
        await command.ExecuteNonQueryAsync();
    }
}


