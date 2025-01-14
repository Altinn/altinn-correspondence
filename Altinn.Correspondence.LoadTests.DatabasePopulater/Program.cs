// See https://aka.ms/new-console-template for more information
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.LoadTests.DatabasePopulater;
using Altinn.Correspondence.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;
using System.IO;

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
        var fileStream = File.Open(path, FileMode.Open);
        using var streamReader = new StreamReader(fileStream, true);
        var line = streamReader.ReadLine();
        Console.WriteLine(line);
        line = streamReader.ReadLine();
        Console.WriteLine(line);

        // Execute raw sql to create the tablevar dropOldTableIfExists = appContext.Database.ExecuteSqlRaw(@"
        var dropOldTableIfExists = appContext.Database.ExecuteSqlRaw(@"
            DROP TABLE IF EXISTS correspondence.altinn2party;"
        );

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

        var npgDatabase = appContext.Database;
        using (var connection = (NpgsqlConnection)appContext.Database.GetDbConnection())
        {
            connection.Open();

            using (var command = new NpgsqlCommand(@"
                        INSERT INTO correspondence.altinn2party 
                        (partyid_pk, name, reguserid, authuserid, orgnumber_ak, unitid_pk, unitname)
                        VALUES 
                        (@PartyID_PK, @Name, @RegUserId, @AuthUserId, @OrgNumber_AK, @UnitId_PK, @UnitName)", connection))
            {
                // Prepare command for reuse
                command.Parameters.Add(new NpgsqlParameter("@PartyID_PK", NpgsqlTypes.NpgsqlDbType.Varchar));
                command.Parameters.Add(new NpgsqlParameter("@Name", NpgsqlTypes.NpgsqlDbType.Varchar));
                command.Parameters.Add(new NpgsqlParameter("@RegUserId", NpgsqlTypes.NpgsqlDbType.Varchar));
                command.Parameters.Add(new NpgsqlParameter("@AuthUserId", NpgsqlTypes.NpgsqlDbType.Varchar));
                command.Parameters.Add(new NpgsqlParameter("@OrgNumber_AK", NpgsqlTypes.NpgsqlDbType.Varchar));
                command.Parameters.Add(new NpgsqlParameter("@UnitId_PK", NpgsqlTypes.NpgsqlDbType.Varchar));
                command.Parameters.Add(new NpgsqlParameter("@UnitName", NpgsqlTypes.NpgsqlDbType.Varchar));

                int rowCount = 0;
                int lineCount = 0;

                while (!streamReader.EndOfStream)
                {
                    var row = streamReader.ReadLine();
                    lineCount++;

                    var newRow = "";
                    var oldRow = "";

                    int spacesRemoved = 0;
                    do
                    {
                        oldRow = row;
                        newRow = row.Replace("  ", " ");
                        row = newRow;

                    } while (newRow.Length < oldRow.Length);
                    var parts = row.Split(' ');
                    if (lineCount % 10000 == 0)
                    {
                        Console.WriteLine("Currently {0}", lineCount);
                    }
                    if (parts.Length != 8)
                        continue; // Skip invalid rows
                    if (parts[5] == "NULL")
                        continue;

                    command.Parameters["@PartyID_PK"].Value = parts[0] ?? (object)DBNull.Value;
                    command.Parameters["@Name"].Value = parts[1] ?? (object)DBNull.Value;
                    command.Parameters["@RegUserId"].Value = parts[2] ?? (object)DBNull.Value;
                    command.Parameters["@AuthUserId"].Value = parts[3] ?? (object)DBNull.Value;
                    command.Parameters["@OrgNumber_AK"].Value = parts[5] ?? (object)DBNull.Value;
                    command.Parameters["@UnitId_PK"].Value = parts[6] ?? (object)DBNull.Value;
                    command.Parameters["@UnitName"].Value = parts[7] ?? (object)DBNull.Value;

                    command.ExecuteNonQuery();
                    rowCount++;

                    if (rowCount % 10000 == 0)
                    {

                        Console.WriteLine("Currently {0}", rowCount);
                    }
                }
                Console.WriteLine("Added {0} rows to the database", rowCount);
            }
        }
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

        /*var startTimeStamp = DateTime.Now;

        int threadCount = 8; // Configurable number of threads
        int batchSize = correspondenceCount / threadCount;

        // Create tasks for each batch
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            var count = (i == threadCount - 1) ? correspondenceCount : batchSize; // Handle remainder

            tasks.Add(applicationDbContext.Database.ExecuteSqlRawAsync($"SELECT populate_test_database({count});"));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        var endTimeStamp = DateTime.Now;
        var secondsRunTime = (endTimeStamp - startTimeStamp).TotalSeconds;*/
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
        Console.WriteLine("Successfully filled database with {0} correspondence records in {1} seconds for a rate of {2} correspondences/second", correspondenceCount* threadCount, secondsRunTime, correspondenceCount* threadCount / secondsRunTime);

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


