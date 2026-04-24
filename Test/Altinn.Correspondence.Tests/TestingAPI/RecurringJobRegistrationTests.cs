using Altinn.Correspondence.API.Helpers;
using Altinn.Correspondence.Core.Options;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingAPI;

public class RecurringJobRegistrationTests
{
    [Fact]
    public void Register_AddsMaskinportenRotationJob_WhenEnabled()
    {
        var recurringJobManager = new Mock<IRecurringJobManager>();
        var services = new ServiceCollection()
            .AddSingleton(recurringJobManager.Object)
            .BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{nameof(MaskinportenJwkRotationSettings)}:Enabled"] = "true",
                [$"{nameof(MaskinportenJwkRotationSettings)}:CronExpression"] = "0 0 1 * *"
            })
            .Build();

        RecurringJobRegistration.Register(services, configuration, NullLogger.Instance);

        Assert.Contains(recurringJobManager.Invocations, invocation =>
            invocation.Method.Name == nameof(IRecurringJobManager.AddOrUpdate)
            && invocation.Arguments.Count > 2
            && invocation.Arguments[0] as string == RecurringJobRegistration.MaskinportenJwkRotationJobId
            && invocation.Arguments[2] as string == "0 0 1 * *");
    }

    [Fact]
    public void Register_DoesNotAddMaskinportenRotationJob_WhenDisabled()
    {
        var recurringJobManager = new Mock<IRecurringJobManager>();
        var services = new ServiceCollection()
            .AddSingleton(recurringJobManager.Object)
            .BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{nameof(MaskinportenJwkRotationSettings)}:Enabled"] = "false"
            })
            .Build();

        RecurringJobRegistration.Register(services, configuration, NullLogger.Instance);

        Assert.DoesNotContain(recurringJobManager.Invocations, invocation =>
            invocation.Method.Name == nameof(IRecurringJobManager.AddOrUpdate)
            && invocation.Arguments.Count > 0
            && invocation.Arguments[0] as string == RecurringJobRegistration.MaskinportenJwkRotationJobId);
        Assert.Contains(recurringJobManager.Invocations, invocation =>
            invocation.Method.Name == nameof(IRecurringJobManager.RemoveIfExists)
            && invocation.Arguments.Count > 0
            && invocation.Arguments[0] as string == RecurringJobRegistration.MaskinportenJwkRotationJobId);
    }
}
