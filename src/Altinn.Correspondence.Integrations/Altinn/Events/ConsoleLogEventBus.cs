using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;

using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Altinn.Events;
public class ConsoleLogEventBus : IEventBus
{
    private readonly ILogger<ConsoleLogEventBus> _logger;

    public ConsoleLogEventBus(ILogger<ConsoleLogEventBus> logger)
    {
        _logger = logger;
    }

    public Task Publish(AltinnEventType type, string resourceId, string itemId, string eventSource, string? organizationId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{CloudEventType} event raised on instance {eventSource} {itemId} for party with organization number {organizationId}", type.ToString(), eventSource, itemId, organizationId);
        return Task.CompletedTask;
    }
}
