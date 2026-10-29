using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations;

public sealed class NoOpContainerAppRefreshService : IContainerAppRefreshService
{
    public Task RefreshAsync(string containerAppResourceId, string reason, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
