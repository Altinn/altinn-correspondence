using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.TestRetry
{
    public class TestRetryHandler(
        ILogger<TestRetryHandler> logger)
    {
        private const int MaxRetries = 10;

        [AutomaticRetry(Attempts = MaxRetries)]
        public async Task Process(PerformContext context, CancellationToken cancellationToken = default)
        {
            // Get the actual retry count from Hangfire
            var retryAttempts = context.GetJobParameter<int>("RetryCount");
            logger.LogInformation("TestRetryHandler running. Retry attempt: {retryAttempts} of {MaxRetries}", retryAttempts, MaxRetries);
            
            // Always fail to simulate a job that retries 10 times
            // We want to fail all 10 times, so we check if we've reached the max retries
            if (retryAttempts < MaxRetries)
            {
                var errorMessage = $"TestRetryHandler failed on attempt {retryAttempts + 1} of {MaxRetries + 1}. This is a simulated failure for testing retry behavior. This job will fail all {MaxRetries + 1} times to demonstrate Slack notification spam.";
                logger.LogError(errorMessage);
                throw new Exception(errorMessage);
            }
            
            // If we've reached the max retries, still fail to ensure we get all 10 Slack notifications
            var finalErrorMessage = $"TestRetryHandler failed on final attempt {retryAttempts + 1} of {MaxRetries + 1}. This is the final failure to demonstrate Slack notification spam.";
            logger.LogError(finalErrorMessage);
            throw new Exception(finalErrorMessage);
        }
    }
} 