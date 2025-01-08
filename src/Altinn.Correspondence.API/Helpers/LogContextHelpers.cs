using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Helpers
{
    public static class LogContextHelpers
    {
        public static void EnrichLogsWithInsertCorrespondence(BaseCorrespondenceExt initializeCorrespondenceExt, HttpContext httpContext)
        {
            httpContext.PushProperty("sender", initializeCorrespondenceExt.Sender);
            httpContext.PushProperty("resourceId", initializeCorrespondenceExt.ResourceId);
            httpContext.PushProperty("sendersReference", initializeCorrespondenceExt.SendersReference);
        }

        public static void EnrichLogsWithMigrateCorrespondence(MigrateCorrespondenceExt migrateCorrespondenceExt, HttpContext httpContext)
        {
            httpContext.PushProperty("sender", migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender);
            httpContext.PushProperty("resourceId", migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId);
            httpContext.PushProperty("sendersReference", migrateCorrespondenceExt.CorrespondenceData.Correspondence.SendersReference);
        }
    }
}
