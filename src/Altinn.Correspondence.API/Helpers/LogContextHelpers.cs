using Altinn.Correspondence.API.Models;
using Serilog.Context;

namespace Altinn.Correspondence.Helpers
{
    public static class LogContextHelpers
    {
        public static void EnrichLogsWithInsertCorrespondence(InitiateCorrespondenceExt insertCorrespondenceExt)
        {
            LogContext.PushProperty("sender", insertCorrespondenceExt.Sender);
            LogContext.PushProperty("recipient", insertCorrespondenceExt.Recipient);
            LogContext.PushProperty("resourceId", insertCorrespondenceExt.ResourceId);
            LogContext.PushProperty("sendersReference", insertCorrespondenceExt.SendersReference);
        }
    }
}
