using Altinn.Correspondence.Application.Helpers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using System.Transactions;

namespace Altinn.Correspondence.Tests.TestingFeature;

public class TransactionWithRetriesPolicyTests
{
    private readonly Mock<ILogger> _mockLogger;

    public TransactionWithRetriesPolicyTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [Fact]
    public async Task Execute_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";
        Task<string> Operation(CancellationToken ct) => Task.FromResult(expectedResult);

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        VerifyNoWarningsLogged();
    }

    [Fact]
    public async Task Execute_OperationSucceedsAfterOneRetry_ReturnsResult()
    {
        // Arrange
        var expectedResult = 42;
        var attemptCount = 0;
        Task<int> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new PostgresException("Transient error", "", "", "40001"); // Serialization failure
            }
            return Task.FromResult(expectedResult);
        }

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
        VerifyWarningLoggedTimes(1);
    }

    [Fact]
    public async Task Execute_TransactionAbortedException_RetriesAndSucceeds()
    {
        // Arrange
        var expectedResult = "recovered";
        var attemptCount = 0;
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount <= 2)
            {
                throw new TransactionAbortedException("Transaction aborted due to deadlock");
            }
            return Task.FromResult(expectedResult);
        }

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(3, attemptCount);
        VerifyWarningLoggedTimes(2);
    }

    [Fact]
    public async Task Execute_DbUpdateConcurrencyException_RetriesAndSucceeds()
    {
        // Arrange
        var expectedResult = true;
        var attemptCount = 0;
        Task<bool> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new DbUpdateConcurrencyException("Concurrency conflict");
            }
            return Task.FromResult(expectedResult);
        }

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.True(result);
        Assert.Equal(2, attemptCount);
        VerifyWarningLoggedTimes(1);
    }

    [Fact]
    public async Task Execute_BackgroundJobClientException_RetriesAndSucceeds()
    {
        // Arrange
        var expectedResult = Guid.NewGuid();
        var attemptCount = 0;
        Task<Guid> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new BackgroundJobClientException("Hangfire job creation failed", new Exception("Inner exception"));
            }
            return Task.FromResult(expectedResult);
        }

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
        VerifyWarningLoggedTimes(1);
    }

    [Fact]
    public async Task Execute_PostgreSqlDistributedLockException_RetriesAndSucceeds()
    {
        // Arrange
        var expectedResult = DateTime.UtcNow;
        var attemptCount = 0;
        Task<DateTime> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new PostgreSqlDistributedLockException("Could not acquire lock");
            }
            return Task.FromResult(expectedResult);
        }

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount);
        VerifyWarningLoggedTimes(1);
    }

    [Fact]
    public async Task Execute_ExhaustsAllRetries_ThrowsException()
    {
        // Arrange
        var attemptCount = 0;
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            throw new PostgresException("Persistent error", "", "", "40001");
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object)
        );

        Assert.Contains("Persistent error", exception.Message);
        Assert.Equal(6, attemptCount); // Initial attempt + 5 retries
        VerifyWarningLoggedTimes(5);
        VerifyErrorLogged();
    }

    [Fact]
    public async Task Execute_NonRetryableException_ThrowsImmediately()
    {
        // Arrange
        var attemptCount = 0;
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            throw new InvalidOperationException("Non-retryable error");
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object)
        );

        Assert.Equal("Non-retryable error", exception.Message);
        Assert.Equal(1, attemptCount); // Should not retry
        VerifyNoWarningsLogged();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task Execute_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var attemptCount = 0;
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("should not reach");
        }

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object, cts.Token)
        );

        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task Execute_MultipleRetriesWithDifferentExceptions_RetriesAll()
    {
        // Arrange
        var expectedResult = "final success";
        var attemptCount = 0;
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

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(4, attemptCount);
        VerifyWarningLoggedTimes(3);
    }

    [Fact]
    public async Task RetryPolicy_HasCorrectRetryCount()
    {
        // Arrange & Act
        var policy = TransactionWithRetriesPolicy.RetryPolicy(_mockLogger.Object);

        // Assert
        // Polly doesn't expose retry count directly, but we can verify by execution
        var attemptCount = 0;
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await policy.ExecuteAsync(() =>
            {
                attemptCount++;
                throw new PostgresException("Test", "", "", "40001");
            });
        });

        // Should be initial attempt + 5 retries = 6 total
        Assert.Equal(6, attemptCount);
    }

    [Fact]
    public async Task RetryPolicy_UsesExponentialBackoff()
    {
        // Arrange
        var policy = TransactionWithRetriesPolicy.RetryPolicy(_mockLogger.Object);
        var attemptTimes = new List<DateTime>();

        // Act
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await policy.ExecuteAsync(() =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                throw new PostgresException("Test", "", "", "40001");
            });
        });

        // Assert - verify delays are increasing (exponential backoff)
        // First retry should be ~100ms, second ~200ms, third ~400ms, etc.
        // We check that each delay is longer than the previous (allowing some variance)
        Assert.True(attemptTimes.Count >= 4, "Should have at least 4 attempts to verify backoff pattern");

        for (int i = 1; i < Math.Min(attemptTimes.Count - 1, 4); i++)
        {
            var previousDelay = (attemptTimes[i] - attemptTimes[i - 1]).TotalMilliseconds;
            var currentDelay = (attemptTimes[i + 1] - attemptTimes[i]).TotalMilliseconds;

            // Current delay should be roughly 2x previous (with some tolerance for execution time)
            Assert.True(currentDelay > previousDelay * 1.5, 
                $"Expected exponential backoff: attempt {i + 1} delay ({currentDelay}ms) should be > {previousDelay * 1.5}ms");
        }
    }

    [Fact]
    public async Task Execute_ComplexObjectResult_ReturnsCorrectly()
    {
        // Arrange
        var expectedResult = new TestResult
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Count = 42,
            Timestamp = DateTime.UtcNow
        };

        Task<TestResult> Operation(CancellationToken ct) => Task.FromResult(expectedResult);

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult.Id, result.Id);
        Assert.Equal(expectedResult.Name, result.Name);
        Assert.Equal(expectedResult.Count, result.Count);
        Assert.Equal(expectedResult.Timestamp, result.Timestamp);
    }

    [Fact]
    public async Task Execute_LongRunningOperation_CompletesSuccessfully()
    {
        // Arrange
        var expectedResult = "completed";
        Task<string> Operation(CancellationToken ct) => Task.Run(async () =>
        {
            await Task.Delay(500, ct); // Simulate work
            return expectedResult;
        }, ct);

        // Act
        var result = await TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task Execute_RetriesUpToMaximumOf5Times()
    {
        // Arrange
        var attemptCount = 0;
        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            throw new DbUpdateConcurrencyException($"Attempt {attemptCount}");
        }

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => TransactionWithRetriesPolicy.Execute(Operation, _mockLogger.Object)
        );

        // Should be: 1 initial attempt + 5 retries = 6 total
        Assert.Equal(6, attemptCount);
        VerifyWarningLoggedTimes(5); // Only retries are logged as warnings
    }

    private void VerifyNoWarningsLogged()
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private void VerifyWarningLoggedTimes(int times)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(times));
    }

    private void VerifyErrorLogged()
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private class TestResult
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
