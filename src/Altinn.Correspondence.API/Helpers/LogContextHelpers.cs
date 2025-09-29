using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Common.Helpers;
using Serilog.Context;

namespace Altinn.Correspondence.Helpers
{
    public static class LogContextHelpers
    {
        public static void EnrichLogsWithInsertCorrespondence(BaseCorrespondenceExt initializeCorrespondenceExt)
        {
            LogContext.PushProperty("sender", initializeCorrespondenceExt.Sender.WithoutPrefix());
            LogContext.PushProperty("resourceId", initializeCorrespondenceExt.ResourceId);
            LogContext.PushProperty("sendersReference", initializeCorrespondenceExt.SendersReference);
        }

        public static void EnrichLogsWithMigrateCorrespondence(MigrateCorrespondenceExt migrateCorrespondenceExt)
        {
            LogContext.PushProperty("sender", migrateCorrespondenceExt.CorrespondenceData.Correspondence.Sender.WithoutPrefix());
            LogContext.PushProperty("resourceId", migrateCorrespondenceExt.CorrespondenceData.Correspondence.ResourceId);
            LogContext.PushProperty("sendersReference", migrateCorrespondenceExt.CorrespondenceData.Correspondence.SendersReference);
        }
    }
}
