using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Hangfire
{
    public class BackgroundJobFiltersTests
    {
        [Fact]
        public void ClientFilter_StampsOriginParameter_WhenAmbientOriginIsSet()
        {
            using var factory = new UnitWebApplicationFactory(_ => { });
            var storage = factory.Services.GetRequiredService<JobStorage>();
            var client = factory.Services.GetRequiredService<IBackgroundJobClient>();
            BackgroundJobContext.Origin = "migrate";
            var jobId = client.Enqueue(() => Console.WriteLine("test"));
            BackgroundJobContext.Origin = null;
            using var connection = storage.GetConnection();
            var origin = connection.GetJobParameter(jobId, "Origin");
            Assert.Equal("migrate", origin?.Trim('"'));
        }

        [Fact]
        public async Task ServerAndClientFilters_PropagateOrigin_ToChildJobs()
        {
            using var factory = new UnitWebApplicationFactory(_ => { });
            var client = factory.Services.GetRequiredService<IBackgroundJobClient>();

            OriginProbe.Reset();

            try
            {
                BackgroundJobContext.Origin = "migrate";
                _ = client.Enqueue<PropagationJobs>(x => x.ParentEnqueueChild());

                var observedOrigin = await OriginProbe.WaitForOriginAsync(TimeSpan.FromSeconds(60));
                Assert.Equal("migrate", observedOrigin);
            }
            finally
            {
                BackgroundJobContext.Origin = null;
            }
        }

        public class PropagationJobs(IBackgroundJobClient client)
        {
            private readonly IBackgroundJobClient _client = client;

            public void ParentEnqueueChild()
            {
                var childId = _client.Enqueue(() => OriginProbe.Capture());
            }
        }

        public static class OriginProbe
        {
            private static readonly object _lock = new();
            private static TaskCompletionSource<string?> _tcs = CreateTcs();

            public static volatile string? LastObservedOrigin;

            private static TaskCompletionSource<string?> CreateTcs() =>
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public static void Reset()
            {
                lock (_lock)
                {
                    LastObservedOrigin = null;
                    _tcs = CreateTcs();
                }
            }

            public static void Capture()
            {
                LastObservedOrigin = BackgroundJobContext.Origin;
                // Best-effort signal; safe if already completed.
                _tcs.TrySetResult(LastObservedOrigin);
            }

            public static async Task<string?> WaitForOriginAsync(TimeSpan timeout)
            {
                Task<string?> task;
                lock (_lock)
                {
                    task = _tcs.Task;
                }

                var completed = await Task.WhenAny(task, Task.Delay(timeout));
                if (completed != task)
                {
                    throw new TimeoutException("Timed out waiting for Hangfire child job to capture origin.");
                }

                return await task;
            }
        }
    }
}