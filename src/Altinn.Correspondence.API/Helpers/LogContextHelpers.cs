using Altinn.Correspondence.API.Models;
using Serilog.Context;

namespace Altinn.Correspondence.Helpers
{
    public static class LogContextHelpers
    {
        public static void EnrichLogsWithInsertCorrespondence(InitializeCorrespondenceExt initializeCorrespondenceExt)
        {
            LogContext.PushProperty("recipient", initializeCorrespondenceExt.Recipient);
            EnrichLogsWithInsertBaseCorrespondence((BaseCorrespondenceExt)initializeCorrespondenceExt);
        }
        public static void EnrichLogsWithInsertBaseCorrespondence(BaseCorrespondenceExt initializeCorrespondenceExt)
        {
            LogContext.PushProperty("sender", initializeCorrespondenceExt.Sender);
            LogContext.PushProperty("resourceId", initializeCorrespondenceExt.ResourceId);
            LogContext.PushProperty("sendersReference", initializeCorrespondenceExt.SendersReference);
        }
    }
}
