using Altinn.Correspondence.Core.Models.Dialogporten;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.OneTimeJobs;

public class DialogportenFixes(
    ILogger<DialogportenFixes> logger,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    IOneTimeFixesRepository oneTimeFixesRepository)
{

    [AutomaticRetry(Attempts = 0)]
    public async Task ScheduleRecipientNameFixForAll(CancellationToken cancellationToken)
    {
        var correspondences = await oneTimeFixesRepository.GetCorrespondenceForNameFix(cancellationToken);
        foreach (var correspondence in correspondences)
        {
            var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
            if (dialogId != null)
                backgroundJobClient.Enqueue<DialogportenFixes>(service => service.ProcessSingleRecipientNameFix(dialogId));
        }

    }

    public async Task ProcessSingleRecipientNameFix(string dialogId)
    {
        var patchData = new PatchData
        {
            operationType = "delete",
            op = "remove",
            path = "/content/sendersName",
        };
        await dialogportenService.PatchData(dialogId, patchData);

    }

}
