using Serilog.Core;
using Serilog.Events;

namespace Altinn.Correspondence.API.Helpers;

public class PropertyEnricher : ILogEventEnricher
{
    private readonly Dictionary<string, object> _properties;

    public PropertyEnricher(Dictionary<string, object> properties)
    {
        _properties = properties;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (key, value) in _properties)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(key, value));
        }
    }
}
