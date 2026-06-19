using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System.Transactions;

namespace Altinn.Correspondence.Tests.TestingFeature;

public class DatabaseTransactionHelperTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test", npgsql =>
                npgsql.ExecutionStrategy(dependencies =>
                    new CorrespondenceNpgsqlRetryingExecutionStrategy(dependencies, maxRetryCount: 5)))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulOperation_ReturnsResult()
    {
        var expectedResult = "success";
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct) => Task.FromResult(expectedResult);

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteAsync_OperationSucceedsAfterOneRetry_ReturnsResult()
    {
        var expectedResult = 42;
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<int> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new PostgresException("Transient error", "", "", "40001");
            }
            return Task.FromResult(expectedResult);
        }

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_TransactionAbortedException_RetriesAndSucceeds()
    {
        var expectedResult = "recovered";
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount <= 2)
            {
                throw new TransactionAbortedException("Transaction aborted due to deadlock");
            }
            return Task.FromResult(expectedResult);
        }

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult, result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateConcurrencyException_RetriesAndSucceeds()
    {
        var expectedResult = true;
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<bool> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new DbUpdateConcurrencyException("Concurrency conflict");
            }
            return Task.FromResult(expectedResult);
        }

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.True(result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_BackgroundJobClientException_RetriesAndSucceeds()
    {
        var expectedResult = Guid.NewGuid();
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<Guid> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new BackgroundJobClientException("Hangfire job creation failed", new Exception("Inner exception"));
            }
            return Task.FromResult(expectedResult);
        }

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_PostgreSqlDistributedLockException_RetriesAndSucceeds()
    {
        var expectedResult = DateTime.UtcNow;
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<DateTime> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new PostgreSqlDistributedLockException("Could not acquire lock");
            }
            return Task.FromResult(expectedResult);
        }

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsAllRetries_ThrowsException()
    {
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            throw new PostgresException("Persistent error", "", "", "40001");
        }

        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(
            () => DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation)
        );

        Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Contains("Persistent error", exception.InnerException!.Message);
        Assert.Equal(6, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_ThrowsImmediately()
    {
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            throw new InvalidOperationException("Non-retryable error");
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation)
        );

        Assert.Equal("Non-retryable error", exception.Message);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("should not reach");
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation, cts.Token)
        );

        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleRetriesWithDifferentExceptions_RetriesAll()
    {
        var expectedResult = "final success";
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            return attemptCount switch
            {
                1 => throw new PostgresException("First error", "", "", "40001"),
                2 => throw new TransactionAbortedException("Second error"),
                3 => throw new DbUpdateConcurrencyException("Third error"),
                _ => Task.FromResult(expectedResult)
            };
        }

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult, result);
        Assert.Equal(4, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesUpToMaximumOf5Times()
    {
        var attemptCount = 0;
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            throw new DbUpdateConcurrencyException($"Attempt {attemptCount}");
        }

        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(
            () => DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation)
        );

        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);

        Assert.Equal(6, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsesExponentialBackoff()
    {
        var attemptCount = 0;
        var attemptTimes = new List<DateTime>();
        await using var dbContext = CreateDbContext();
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            attemptTimes.Add(DateTime.UtcNow);
            throw new PostgresException("Test", "", "", "40001");
        }

        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(
            () => DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation)
        );

        Assert.IsType<PostgresException>(exception.InnerException);

        Assert.True(attemptTimes.Count >= 4, "Should have at least 4 attempts to verify backoff pattern");

        for (int i = 1; i < Math.Min(attemptTimes.Count - 1, 4); i++)
        {
            var previousDelay = (attemptTimes[i] - attemptTimes[i - 1]).TotalMilliseconds;
            var currentDelay = (attemptTimes[i + 1] - attemptTimes[i]).TotalMilliseconds;

            Assert.True(currentDelay > previousDelay * 1.5,
                $"Expected exponential backoff: attempt {i + 1} delay ({currentDelay}ms) should be > {previousDelay * 1.5}ms");
        }
    }

    [Fact]
    public async Task ExecuteAsync_ComplexObjectResult_ReturnsCorrectly()
    {
        var expectedResult = new TestResult
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Count = 42,
            Timestamp = DateTime.UtcNow
        };
        await using var dbContext = CreateDbContext();
        Task<TestResult> Operation(CancellationToken ct) => Task.FromResult(expectedResult);

        var result = await DatabaseTransactionHelper.ExecuteAsync(dbContext, Operation);

        Assert.Equal(expectedResult.Id, result.Id);
        Assert.Equal(expectedResult.Name, result.Name);
        Assert.Equal(expectedResult.Count, result.Count);
        Assert.Equal(expectedResult.Timestamp, result.Timestamp);
    }

    private class TestResult
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
