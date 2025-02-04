using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Altinn.Correspondence.API.Helpers;

public class PropertyPropagationEnricher : ILogEventEnricher
{
    private readonly HashSet<string> _propertiesToPropagate;

    public PropertyPropagationEnricher(params string[] propertiesToPropagate)
    {
        _propertiesToPropagate = new HashSet<string>(propertiesToPropagate);
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var propertyName in _propertiesToPropagate)
        {
            if (logEvent.Properties.TryGetValue(propertyName, out var propertyValue))
            {
                LogContext.PushProperty(propertyName, propertyValue);
            }
        }
    }
}

