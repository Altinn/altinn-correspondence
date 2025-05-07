using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Redlock;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Altinn.Correspondence.Tests.TestingIntegrations.RedLock
{
    public class DistributedLockHelperTests
    {
        private readonly Mock<IOptions<GeneralSettings>> _mockGeneralSettings;
        private readonly Mock<ILogger<DistributedLockHelper>> _mockLogger;
        private readonly GeneralSettings _generalSettings;

        public DistributedLockHelperTests()
        {
            _mockLogger = new Mock<ILogger<DistributedLockHelper>>();
            _generalSettings = new GeneralSettings
            {
                RedisConnectionString = "localhost:6379"
            };
            _mockGeneralSettings = new Mock<IOptions<GeneralSettings>>();
            _mockGeneralSettings.Setup(x => x.Value).Returns(_generalSettings);
        }

        [Fact]
        public async Task ExecuteWithConditionalLockAsync_WhenSkipConditionInitiallyTrue_SkipsLockAcquisition()
        {
            // Arrange
            var helper = new DistributedLockHelper(_mockGeneralSettings.Object, _mockLogger.Object);
            var actionExecuted = false;
            var checkCalled = false;
            
            Task<bool> shouldSkipCheck(CancellationToken _)
            {
                checkCalled = true;
                return Task.FromResult(true);
            }
            
            Task action(CancellationToken _)
            {
                actionExecuted = true;
                return Task.CompletedTask;
            }

            // Act
            var (wasSkipped, lockAcquired) = await helper.ExecuteWithConditionalLockAsync(
                $"test-lock-{Guid.NewGuid()}",
                shouldSkipCheck,
                action,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(wasSkipped);
            Assert.False(lockAcquired);
            Assert.True(checkCalled);
            Assert.False(actionExecuted);
        }

        [Fact]
        public async Task ExecuteWithConditionalLockAsync_WhenLockAcquiredButConditionBecameTrue_SkipsAction()
        {
            // Arrange
            var helper = new DistributedLockHelper(_mockGeneralSettings.Object, _mockLogger.Object);
            var actionExecuted = false;
            var checkCallCount = 0;
            
            Task<bool> shouldSkipCheck(CancellationToken _)
            {
                checkCallCount++;
                // Return false on first call, true on second call
                return Task.FromResult(checkCallCount > 1);
            }
            
            Task action(CancellationToken _)
            {
                actionExecuted = true;
                return Task.CompletedTask;
            }

            // Act
            var (wasSkipped, lockAcquired) = await helper.ExecuteWithConditionalLockAsync(
                $"test-lock-{Guid.NewGuid()}",
                shouldSkipCheck,
                action,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(wasSkipped);
            Assert.True(lockAcquired);
            Assert.False(actionExecuted);
            Assert.Equal(2, checkCallCount);
        }

        [Fact]
        public async Task ExecuteWithConditionalLockAsync_WhenLockAcquiredAndConditionFalse_ExecutesAction()
        {
            // Arrange
            var helper = new DistributedLockHelper(_mockGeneralSettings.Object, _mockLogger.Object);
            var actionExecuted = false;
            
            Task<bool> shouldSkipCheck(CancellationToken _)
            {
                return Task.FromResult(false);
            }
            
            Task action(CancellationToken _)
            {
                actionExecuted = true;
                return Task.CompletedTask;
            }

            // Act
            var (wasSkipped, lockAcquired) = await helper.ExecuteWithConditionalLockAsync(
                $"test-lock-{Guid.NewGuid()}",
                shouldSkipCheck,
                action,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.False(wasSkipped);
            Assert.True(lockAcquired);
            Assert.True(actionExecuted);
        }

        [Fact]
        public async Task ExecuteWithConditionalLockAsync_WhenActionThrowsException_PropagatesException()
        {
            // Arrange
            var helper = new DistributedLockHelper(_mockGeneralSettings.Object, _mockLogger.Object);
            var expectedExceptionMessage = "Test exception";
            
            Task<bool> shouldSkipCheck(CancellationToken _)
            {
                return Task.FromResult(false);
            }
            
            Task action(CancellationToken _)
            {
                throw new InvalidOperationException(expectedExceptionMessage);
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                helper.ExecuteWithConditionalLockAsync(
                    $"test-lock-{Guid.NewGuid()}",
                    shouldSkipCheck,
                    action,
                    cancellationToken: CancellationToken.None));
            
            Assert.Equal(expectedExceptionMessage, exception.Message);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error executing action with lock")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                    Times.Once
                );
        }
    }
} 