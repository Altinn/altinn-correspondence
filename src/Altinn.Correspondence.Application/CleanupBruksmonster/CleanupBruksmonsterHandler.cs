using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterHandler(
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupBruksmonsterHandler> logger,
    IDialogportenService dialogportenService,
    ICorrespondenceRepository correspondenceRepository,
	IIdempotencyKeyRepository idempotencyKeyRepository,
	IAttachmentRepository attachmentRepository
) : IHandler<CleanupBruksmonsterRequest, CleanupBruksmonsterResponse>
{
    public async Task<OneOf<CleanupBruksmonsterResponse, Error>> Process(CleanupBruksmonsterRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of bruksmonster test data");
        var resourceId = "correspondence-bruksmonstertester-ressurs";
        var minAge = DateTimeOffset.UtcNow;
        if (request.MinAgeDays.HasValue)
        {
            minAge = minAge.Subtract(TimeSpan.FromDays(request.MinAgeDays.Value));
        }

        var correspondenceIds = new List<Guid>();
        var attachmentIds = new List<Guid>();
        if (request.TestRunId.HasValue)
        {
            correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByResourceIdAndTestRunId(resourceId, request.TestRunId.Value, minAge, cancellationToken);
            attachmentIds = await attachmentRepository.GetAttachmentIdsOnResourceAndTestRunId(resourceId, request.TestRunId.Value, minAge, cancellationToken);
        }
        else 
        {
            correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByResourceId(resourceId, minAge, cancellationToken);
            attachmentIds = await attachmentRepository.GetAttachmentIdsOnResource(resourceId, minAge, cancellationToken);
        }

		return await TransactionWithRetriesPolicy.Execute<CleanupBruksmonsterResponse>(async (ct) =>
        {
            
            var deleteDialogsJobId = backgroundJobClient.Enqueue<CleanupBruksmonsterHandler>(h => h.PurgeCorrespondenceDialogs(correspondenceIds));
			var deleteCorrespondencesJobId = backgroundJobClient.ContinueJobWith<CleanupBruksmonsterHandler>(deleteDialogsJobId, h => h.PurgeCorrespondences(correspondenceIds, attachmentIds, resourceId, CancellationToken.None));
            await Task.CompletedTask;

            var resp = new CleanupBruksmonsterResponse
            {
                ResourceId = resourceId,
                CorrespondencesFound = correspondenceIds.Count,
                AttachmentsFound = attachmentIds.Count,
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
            logger.LogInformation("Purging correspondence dialog {correspondenceId} by cleanup bruksmonster", correspondenceId);
            await dialogportenService.PurgeCorrespondenceDialog(correspondenceId);
        }
    }

	public async Task PurgeCorrespondences(List<Guid> correspondenceIds, List<Guid> attachmentIds, string resourceId, CancellationToken cancellationToken)
    {
        await TransactionWithRetriesPolicy.Execute<Task>(async (ct) =>
        {
            await idempotencyKeyRepository.DeleteByCorrespondenceIds(correspondenceIds, cancellationToken);
            await correspondenceRepository.HardDeleteCorrespondencesByIds(correspondenceIds, cancellationToken);

			var attachments = await attachmentRepository.GetAttachmentsByIds(attachmentIds, true, cancellationToken);
			foreach (var attachment in attachments)
			{
				backgroundJobClient.Enqueue<IStorageRepository>(repository => repository.PurgeAttachment(attachment.Id, attachment.StorageProvider, CancellationToken.None));
			}

			int deletedAttachments = await attachmentRepository.HardDeleteOrphanedAttachments(attachmentIds, cancellationToken);
			logger.LogInformation("Deleted {deletedAttachments} orphaned attachments of {totalAttachments} on resource {resourceId} by cleanup bruksmonster", deletedAttachments, attachmentIds.Count, resourceId);

            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}