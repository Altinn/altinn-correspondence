using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Hangfire;
using Hangfire.Common;

namespace Altinn.Correspondence.Tests.Invariants;

// The publish job is registered as a Hangfire continuation of the jobs that run before it, and continuations only run once
// the parent reaches a final state. Succeeded and Deleted are final in Hangfire - Failed is not - so every job the publish
// job transitively continues from must use OnAttemptsExceeded = Delete, or the correspondence is stuck in status
// Initialized instead of getting Failed. See https://github.com/Altinn/altinn-correspondence/issues/2061
public class PrePublishJobFinalStateTests
{
    public static TheoryData<string, Job> PrePublishJobs => new()
    {
        {
            "InitializeCorrespondencesHandler.CreateDialogportenDialog",
            Job.FromExpression<InitializeCorrespondencesHandler>(handler => handler.CreateDialogportenDialog(Guid.Empty))
        },
        {
            "InitializeCorrespondencesHandler.CreateDialogportenTransmission",
            Job.FromExpression<InitializeCorrespondencesHandler>(handler => handler.CreateDialogportenTransmission(Guid.Empty))
        },
        {
            "CreateNotificationOrderHandler.Process",
            Job.FromExpression<CreateNotificationOrderHandler>(handler => handler.Process((CreateNotificationOrderRequest)null!, CancellationToken.None))
        },
    };

    [Theory]
    [MemberData(nameof(PrePublishJobs))]
    public void PrePublishJob_IsDeletedWhenAttemptsAreExceeded(string jobName, Job job)
    {
        // Resolving the filters the way the Hangfire server does verifies that the method level attribute actually replaces
        // Hangfire's global AutomaticRetryAttribute, rather than being added alongside it and retrying the job twice.
        var automaticRetries = JobFilterProviders.Providers.GetFilters(job)
            .Select(filter => filter.Instance)
            .OfType<AutomaticRetryAttribute>()
            .ToList();

        var automaticRetry = Assert.Single(automaticRetries);
        Assert.Equal(AttemptsExceededAction.Delete, automaticRetry.OnAttemptsExceeded);
        Assert.True(
            automaticRetry.Attempts > 0,
            $"{jobName} must allow at least one retry attempt for OnAttemptsExceeded to take effect.");
    }
}
