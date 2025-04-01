using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Linq;

namespace Altinn.Correspondence.API.Helpers;

public class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationId", activity.Id));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        }
    }
} 