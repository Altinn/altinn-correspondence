using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Core.Services;

public interface IEventBus
{
    Task Publish(AltinnEventType type, string resourceId, string itemId, string eventSource, string? organizationId = null, CancellationToken cancellationToken = default);
}
