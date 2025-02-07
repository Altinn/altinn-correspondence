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

    public Task Publish(AltinnEventType type, string resourceId, string itemId, string eventSource, string? recipientId = null, CancellationToken cancellationToken = default, bool inBackground = true)
    {
        _logger.LogInformation("{CloudEventType} event raised on instance {eventSource} {itemId} for party with organization number or ssn: {recipientId}", type.ToString(), eventSource, itemId, recipientId);
        return Task.CompletedTask;
    }
}
