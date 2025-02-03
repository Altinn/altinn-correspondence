using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Altinn.Correspondence.Common.Helpers;

public static class HttpContextLogExtensions
{
    public static void AddLogProperty(this HttpContext context, string name, object value)
    {
        if (context.Items["LogProperties"] is Dictionary<string, object> properties)
        {
            properties[name] = value;
        }
    }
}
