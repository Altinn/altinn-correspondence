using Altinn.Correspondence.Tests.Fixtures;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Transactions;

namespace Altinn.Correspondence.Tests.Invariants;

[Collection(nameof(CustomWebApplicationTestsCollection))]
public class HangfireStorageCompatibilityTests
{
    // TransactionScope is used to ensure eventual consistency for scheduled background jobs (as used for outbox pattern for posts to events API) 
    // Test ensures that Hangfire implementation is compatible with TransactionScope.

    [Fact]
    public async Task BackgroundJobClient_TransactionScopeCompatible()
    {
        using var testFactory = new UnitWebApplicationFactory((IServiceCollection services) => { });
        using var testDataSource = testFactory.Services.GetRequiredService<NpgsqlDataSource>();

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
                Assert.Equal(0, (long?)command.ExecuteScalar());
                transaction.Complete();
            }
            var postCommitCommand = testDataSource.CreateCommand("select COUNT(job) FROM hangfire.job WHERE id = @jobId");
            postCommitCommand.Parameters.AddWithValue("jobId", parentJobId);
            Assert.Equal(1, (long?)postCommitCommand.ExecuteScalar());
        }
        finally
        {
            try
            {
                await migrateConnection.CloseAsync();
            }
            finally
            {
                migrateConnection.Dispose();
            }
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
