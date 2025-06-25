using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace Altinn.Correspondence.Integrations.Azure
{
    /// <summary>
    /// Filter for requests (and child dependencies) that should not be logged.
    /// </summary>
    public class RequestFilterProcessor : BaseProcessor<Activity>
    {
        private const string RequestKind = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
        private readonly bool _disableTelemetryForMigration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestFilterProcessor"/> class.
        /// </summary>
        public RequestFilterProcessor(bool disableTelemetryForMigration,  IHttpContextAccessor httpContextAccessor = null) : base()
        {
            _disableTelemetryForMigration = disableTelemetryForMigration;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Determine whether to skip a request
        /// </summary>
        public override void OnStart(Activity activity)
        {
            bool skip = false;
            if (activity.OperationName == RequestKind)
            {
                skip = ExcludeRequest(_httpContextAccessor.HttpContext.Request.Path.Value);
            }
            else if (!(activity.Parent?.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ?? true))
            {
                skip = true;
            }

            if (skip)
            {
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }

        /// <summary>
        /// No action on end
        /// </summary>
        /// <param name="activity">xx</param>
        public override void OnEnd(Activity activity)
        {
        }

        private bool ExcludeRequest(string localpath)
        {
            return localpath switch
            {
                var path when _disableTelemetryForMigration && path.TrimEnd('/').EndsWith("/health", StringComparison.OrdinalIgnoreCase) => true,
                "/correspondence/api/v1/migration" => true,
                _ => false
            };
        }
    }
}
