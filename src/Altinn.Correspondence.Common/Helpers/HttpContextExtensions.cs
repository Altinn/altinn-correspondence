using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Altinn.Correspondence.Common.Helpers
{
    public static class HttpContextExtensions
    {
        public static void PushProperty(this HttpContext context, string name, object value)
        {
            if (context.Items["LogContextDisposables"] is List<IDisposable> disposables)
            {
                disposables.Add(LogContext.PushProperty(name, value));
            }
        }
    }
}
