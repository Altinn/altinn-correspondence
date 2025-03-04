using Altinn.Correspondence.Core.Models.Dialogporten;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.OneTimeJobs;

public class DialogportenFixes(
    ILogger<DialogportenFixes> logger,
    IDialogportenService dialogportenService,
    IOneTimeFixesRepository oneTimeFixesRepository)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task ScheduleRecipientNameFixForAll(bool dryRun, CancellationToken cancellationToken)
    {
        var correspondences = await oneTimeFixesRepository.GetCorrespondenceForNameFix(cancellationToken);
        await ProcessDialogBatches(correspondences, GetRecipientNameFixPatches, dryRun, cancellationToken);
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ScheduleReadStatusFixForAll(bool dryRun, CancellationToken cancellationToken)
    {
        var correspondences = await oneTimeFixesRepository.GetCorrespondences(cancellationToken);
        await ProcessDialogBatches(correspondences, GetReadStatusPatches, dryRun, cancellationToken);
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ScheduleConfirmationFixForAll(bool dryRun, CancellationToken cancellationToken)
    {
        var correspondences = await oneTimeFixesRepository.GetCorrespondencesWithoutConfirmation(cancellationToken);
        await ProcessDialogBatches(correspondences, GetConfirmationPatches, dryRun, cancellationToken);
    }

    private async Task ProcessDialogBatches(
        List<CorrespondenceEntity> correspondences,
        Func<CorrespondenceEntity, List<PatchData>> getPatchesFunc,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        // Group correspondences by their dialog ID
        var dialogGroups = new Dictionary<string, List<(CorrespondenceEntity entity, List<PatchData> patches)>>();

        foreach (var entity in correspondences)
        {
            var dialogId = GetDialogId(entity);
            if (dialogId == null)
            {
                logger.LogWarning("Correspondence {correspondenceId} has no DialogportenDialogId", entity.Id);
                continue;
            }

            var patches = getPatchesFunc(entity);
            if (patches.Count > 0)
            {
                if (!dialogGroups.ContainsKey(dialogId))
                {
                    dialogGroups[dialogId] = new List<(CorrespondenceEntity, List<PatchData>)>();
                }

                dialogGroups[dialogId].Add((entity, patches));
            }
        }

        // Count total patches for logging
        int totalCorrespondences = dialogGroups.Sum(g => g.Value.Count);
        int totalPatches = dialogGroups.Sum(g => g.Value.Sum(item => item.patches.Count));

        if (dryRun)
        {
            logger.LogInformation(
                "Dry run, would have patched {correspondenceCount} correspondences across {dialogCount} dialogs with {patchCount} patches",
                totalCorrespondences,
                dialogGroups.Count,
                totalPatches);
            return;
        }

        await ExecuteDialogPatches(dialogGroups, cancellationToken);
    }

    private async Task ExecuteDialogPatches(
        Dictionary<string, List<(CorrespondenceEntity entity, List<PatchData> patches)>> dialogGroups,
        CancellationToken cancellationToken)
    {
        int totalCorrespondences = dialogGroups.Sum(g => g.Value.Count);
        int totalPatches = dialogGroups.Sum(g => g.Value.Sum(item => item.patches.Count));

        logger.LogInformation(
            "Patching {correspondenceCount} correspondences across {dialogCount} dialogs with {patchCount} patches",
            totalCorrespondences,
            dialogGroups.Count,
            totalPatches);

        // For each dialog, execute all patches
        foreach (var (dialogId, correspondenceGroups) in dialogGroups)
        {
            foreach (var (entity, patches) in correspondenceGroups)
            {
                logger.LogDebug("Applying {patchCount} patches for correspondence {correspondenceId} in dialog {dialogId}",
                    patches.Count, entity.Id, dialogId);

                foreach (var patch in patches)
                {
                    await dialogportenService.PatchData(dialogId, patch);
                }
            }
        }
    }

    private string GetDialogId(CorrespondenceEntity entity)
    {
        return entity.ExternalReferences
            .FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)
            ?.ReferenceValue;
    }

    private List<PatchData> GetRecipientNameFixPatches(CorrespondenceEntity entity)
    {
        if (entity.MessageSender == null)
        {
            return [
                new PatchData
                {
                    operationType = "delete",
                    op = "remove",
                    path = "/content/sendersName",
                }
            ];
        }

        return [];
    }

    private List<PatchData> GetReadStatusPatches(CorrespondenceEntity entity)
    {
        return entity.Statuses
            .Where(status => status.Status == CorrespondenceStatus.Read)
            .Select(_ => new PatchData
            {
                operationType = "add",
                op = "add",
                path = "/status/opened",
                value = true.ToString()
            })
            .ToList();
    }

    private List<PatchData> GetConfirmationPatches(CorrespondenceEntity entity)
    {
        if (!entity.IsConfirmationNeeded)
        {
            return [
                new PatchData
                {
                    operationType = "delete",
                    op = "remove",
                    path = "/content/confirmButton",
                }
            ];
        }

        return [];
    }
}