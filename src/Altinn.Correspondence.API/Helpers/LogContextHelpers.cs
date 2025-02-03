using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Altinn.Correspondence.Helpers
{
    public static class LogContextHelpers
    {
        public static void EnrichLogsWithInsertCorrespondence(this HttpContext httpContext, BaseCorrespondenceExt initializeCorrespondenceExt)
        {
            httpContext.AddLogProperty("sender", initializeCorrespondenceExt.Sender.WithoutPrefix());
            httpContext.AddLogProperty("resourceId", initializeCorrespondenceExt.ResourceId);
            httpContext.AddLogProperty("sendersReference", initializeCorrespondenceExt.SendersReference);
        }

        public static void EnrichLogsWithMigrateCorrespondence(this HttpContext httpContext, MigrateCorrespondenceExt migrateCorrespondenceExt)
        {
            httpContext.AddLogProperty("sender", migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender.WithoutPrefix());
            httpContext.AddLogProperty("resourceId", migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId);
            httpContext.AddLogProperty("sendersReference", migrateCorrespondenceExt.CorrespondenceData.Correspondence.SendersReference);
        }
    }
}
