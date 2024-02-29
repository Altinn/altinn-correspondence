using Altinn.Correspondence.Core.Domain.Models;
using Altinn.Correspondence.API.Models;
using Serilog.Context;

namespace Altinn.Correspondence.Helpers
{

    public static class LogContextHelpers
    {
        public static void EnrichLogsWithInsertCorrespondence(InsertCorrespondenceExt insertCorrespondenceExt)
        {
            LogContext.PushProperty("sender", insertCorrespondenceExt.Sender);
            LogContext.PushProperty("recipient", insertCorrespondenceExt.Recipient);
            LogContext.PushProperty("resourceId", insertCorrespondenceExt.ResourceId);
            LogContext.PushProperty("sendersReference", insertCorrespondenceExt.SendersReference);
        }
    }
}
