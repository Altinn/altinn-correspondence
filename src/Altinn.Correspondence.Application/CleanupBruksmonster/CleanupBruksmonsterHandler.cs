using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Hangfire;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence;

namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterHandler(
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupBruksmonsterHandler> logger,
    IDialogportenService dialogportenService,
    ICorrespondenceRepository correspondenceRepository,
	IIdempotencyKeyRepository idempotencyKeyRepository,
	IAttachmentRepository attachmentRepository,
	ApplicationDbContext dbContext
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

        correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByResourceId(resourceId, minAge, cancellationToken);
        attachmentIds = await attachmentRepository.GetAttachmentIdsOnResource(resourceId, minAge, cancellationToken);

		return await DatabaseTransactionHelper.ExecuteAsync(dbContext, async (ct) =>
        {
            var deleteDialogsJobId = backgroundJobClient.Enqueue<CleanupBruksmonsterHandler>(h => h.PurgeCorrespondenceDialogs(correspondenceIds));
			var deleteCorrespondencesJobId = backgroundJobClient.ContinueJobWith<CleanupBruksmonsterHandler>(deleteDialogsJobId, h => h.PurgeCorrespondences(correspondenceIds, attachmentIds, resourceId, CancellationToken.None));
            await Task.CompletedTask;

            logger.LogInformation("Deleting {correspondenceIds.Count} correspondences and {attachmentIds.Count} attachments on resource {resourceId}", correspondenceIds.Count, attachmentIds.Count, resourceId);
            var resp = new CleanupBruksmonsterResponse
            {
                ResourceId = resourceId,
                CorrespondencesFound = correspondenceIds.Count,
                AttachmentsFound = attachmentIds.Count,
                DeleteDialogsJobId = deleteDialogsJobId,
                DeleteCorrespondencesJobId = deleteCorrespondencesJobId
            };
            return resp;
        }, cancellationToken);
    }

    public async Task PurgeCorrespondenceDialogs(List<Guid> correspondenceIds)
    {
        foreach (var correspondenceId in correspondenceIds)
        {
            logger.LogInformation("Purging dialog for correspondence {correspondenceId}", correspondenceId);
            try
            {
                await dialogportenService.PurgeCorrespondenceDialog(correspondenceId);
            }
            catch (ArgumentException ex)
            {
                logger.LogInformation(ex, "dialog already purged for correspondence {correspondenceId}; skipping and continuing with remaining correspondences", correspondenceId);
            }
        }
    }

	public async Task PurgeCorrespondences(List<Guid> correspondenceIds, List<Guid> attachmentIds, string resourceId, CancellationToken cancellationToken)
    {
        await DatabaseTransactionHelper.ExecuteAsync(dbContext, async (ct) =>
        {
            await idempotencyKeyRepository.DeleteByCorrespondenceIds(correspondenceIds, cancellationToken);
            await correspondenceRepository.HardDeleteCorrespondencesByIds(correspondenceIds, cancellationToken);

			var attachments = await attachmentRepository.GetAttachmentsByIds(attachmentIds, true, cancellationToken);
			foreach (var attachment in attachments)
			{
				backgroundJobClient.Enqueue<IStorageRepository>(repository => repository.PurgeAttachment(attachment.Id, attachment.StorageProvider, CancellationToken.None));
			}

			int deletedAttachments = await attachmentRepository.HardDeleteOrphanedAttachments(attachmentIds, cancellationToken);
			logger.LogInformation("Deleted {deletedAttachments} rows/entities for {totalAttachments} attachments on resource {resourceId}", deletedAttachments, attachmentIds.Count, resourceId);

            return Task.CompletedTask;
        }, cancellationToken);
    }
}