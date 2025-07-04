using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Transactions;

namespace Altinn.Correspondence.Tests;
[Collection(nameof(CustomWebApplicationTestsCollection))]
public class HangfireStorageCompatibilityTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // TransactionScope is used to ensure eventual consistency for scheduled background jobs (as used for outbox pattern for posts to events API) 
    // Test ensures that Hangfire implementation is compatible with TransactionScope.
    [Fact]
    public async Task BackgroundJobClient_TransactionScopeCompatible()
    {
        using var testDataSource = _factory.Services.GetRequiredService<NpgsqlDataSource>();

        var migrateConnection = await testDataSource.OpenConnectionAsync();
        try
        {
            PostgreSqlObjectsInstaller.Install(migrateConnection);
            var connectionFactory = new TestConnectionFactory(testDataSource);
            var jobStorage = new PostgreSqlStorage(connectionFactory);
            var backgroundJobClient = new BackgroundJobClient(jobStorage);
            long parentJobId;
            var outsideConnection = await testDataSource.OpenConnectionAsync();
            using (var transaction = new TransactionScope(TransactionScopeOption.Required))
            {
                var parentJob = backgroundJobClient.Enqueue(() => Console.WriteLine("Hello World!"));
                parentJobId = long.Parse(parentJob);
                var command = outsideConnection.CreateCommand();
                command.CommandText = "select COUNT(job) FROM hangfire.job WHERE id = @jobId";
                command.Parameters.AddWithValue("jobId", parentJobId);
                var result = command.ExecuteScalar();
                Assert.True((long)command.ExecuteScalar() == 0);
                transaction.Complete();
            }
            var postCommitCommand = testDataSource.CreateCommand("select COUNT(job) FROM hangfire.job WHERE id = @jobId");
            postCommitCommand.Parameters.AddWithValue("jobId", parentJobId);
            Assert.True((long)postCommitCommand.ExecuteScalar() == 1);
        }
        finally
        {
            await migrateConnection.CloseAsync();
            migrateConnection.Dispose();
            _factory.Dispose();
        }
    }

    internal class TestConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
    {
        public NpgsqlConnection GetOrCreateConnection()
        {
            return dataSource.CreateConnection();
        }
    }
}
