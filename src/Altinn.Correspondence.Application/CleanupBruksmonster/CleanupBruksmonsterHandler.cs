using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Hangfire;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterHandler(
    IOptions<GeneralSettings> generalSettings,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupBruksmonsterHandler> logger,
    IDialogportenService dialogportenService,
    ICorrespondenceRepository correspondenceRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository
) : IHandler<CleanupBruksmonsterResponse>
{
    public async Task<OneOf<CleanupBruksmonsterResponse, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var resourceId = generalSettings.Value.BruksmonsterTestsResourceId;
        if (string.IsNullOrEmpty(resourceId))
        {
            return MaintenanceErrors.ResourceIdForBruksmonsterTestsNotConfigured;
        }
        if (resourceId != "correspondence-bruksmonstertester-ressurs")
        {
            return MaintenanceErrors.InvalidResourceIdForBruksmonsterTests;
        }

        var correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByResourceId(resourceId, cancellationToken);

        return await TransactionWithRetriesPolicy.Execute<CleanupBruksmonsterResponse>(async (ct) =>
        {
            var deleteDialogsJobId = backgroundJobClient.Enqueue<CleanupBruksmonsterHandler>(h => h.PurgeCorrespondenceDialogs(correspondenceIds));
            var deleteCorrespondencesJobId = backgroundJobClient.ContinueJobWith<CleanupBruksmonsterHandler>(deleteDialogsJobId, h => h.PurgeCorrespondences(correspondenceIds, CancellationToken.None));
            await Task.CompletedTask;

            var resp = new CleanupBruksmonsterResponse
            {
                ResourceId = resourceId,
                CorrespondencesFound = correspondenceIds.Count,
                DeleteDialogsJobId = deleteDialogsJobId,
                DeleteCorrespondencesJobId = deleteCorrespondencesJobId
            };
            return resp;
        }, logger, cancellationToken);
    }

    public async Task PurgeCorrespondenceDialogs(List<Guid> correspondenceIds)
    {
        foreach (var correspondenceId in correspondenceIds)
        {
            await dialogportenService.PurgeCorrespondenceDialog(correspondenceId);
        }
    }

    public async Task PurgeCorrespondences(List<Guid> correspondenceIds, CancellationToken cancellationToken)
    {
        await TransactionWithRetriesPolicy.Execute<Task>(async (ct) =>
        {
            await idempotencyKeyRepository.DeleteByCorrespondenceIds(correspondenceIds, cancellationToken);
            await correspondenceRepository.HardDeleteCorrespondencesByIds(correspondenceIds, cancellationToken);

            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}

