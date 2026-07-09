using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.ExpireAttachment;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UnreadConfidentialCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesHandler(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    IHybridCacheWrapper hybridCacheWrapper,
    HangfireScheduleHelper hangfireScheduleHelper,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IHostEnvironment hostEnvironment,
    InitializeCorrespondenceValidationHelper initializeCorrespondenceValidationHelper,
    ILogger<InitializeCorrespondencesHandler> logger,
    ApplicationDbContext dbContext) : IHandler<InitializeCorrespondencesRequest, InitializeCorrespondencesResponse>
{

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing correspondence initialization request for resource {ResourceId}", request.Correspondence.ResourceId);

        if (request.IdempotentKey.HasValue)
        {
            if (request.Recipients != null && request.Recipients.Count > 1)
            {
                logger.LogWarning("IdempotencyKey cannot be used with multiple recipients");
                return CorrespondenceErrors.IdempotencyKeyNotAllowedWithMultipleRecipients;
            }
            logger.LogInformation("Checking idempotency key {Key}", request.IdempotentKey.Value);
            var result = await DatabaseTransactionHelper.ExecuteAsync<OneOf<InitializeCorrespondencesResponse, Error>>(dbContext, async (cancellationToken) =>
            {
                var existingKey = await idempotencyKeyRepository.GetByIdAsync(request.IdempotentKey.Value, cancellationToken);
                if (existingKey != null)
                {
                    logger.LogWarning("Duplicate idempotency key {Key} found", request.IdempotentKey.Value);
                    return CorrespondenceErrors.DuplicateInitCorrespondenceRequest;
                }
                return new OneOf<InitializeCorrespondencesResponse, Error>();
            }, cancellationToken);

            if (result.IsT1)
            {
                return result.AsT1;
            }
        }

        var validationResult = await initializeCorrespondenceValidationHelper.ValidatePrepareDataAndUploadAttachments(request, user, cancellationToken);
        if (validationResult.IsT1)
        {
            return validationResult.AsT1;
        }

        var validatedData = validationResult.AsT0;
        logger.LogInformation("Initializing correspondences with validated data");
        return await DatabaseTransactionHelper.ExecuteAsync<OneOf<InitializeCorrespondencesResponse, Error>>(dbContext, async (cancellationToken) =>
        {
            return await InitializeCorrespondences(
                request,
                validatedData,
                cancellationToken);
        }, cancellationToken);
    }

    private async Task<OneOf<InitializeCorrespondencesResponse, Error>> InitializeCorrespondences(
        InitializeCorrespondencesRequest request,
        InitializeCorrespondenceValidationHelper.ValidatedData validatedData,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing {correspondenceCount} correspondences for {resourceId}",
            request.Recipients.Count,
            request.Correspondence.ResourceId.SanitizeForLogging());

        var correspondences = new List<CorrespondenceEntity>();
        var serviceOwnerOrgNumber = validatedData.ServiceOwnerOrgNumber;
        foreach (var recipient in request.Recipients)
        {
            var isReserved = validatedData.ReservedRecipients.Contains(recipient.WithoutPrefix());
            var recipientParty = validatedData.RecipientDetails.FirstOrDefault(r => r.GetPersonIdentifier() == recipient.WithoutPrefix() || r.GetOrganizationIdentifier() == recipient.WithoutPrefix());
            var correspondence = await initializeCorrespondenceHelper.MapToCorrespondenceEntityAsync(request, recipient, validatedData.AttachmentsToBeUploaded, validatedData.PartyUuid, recipientParty, isReserved, serviceOwnerOrgNumber, cancellationToken);
            correspondences.Add(correspondence);
        }
        await correspondenceRepository.CreateCorrespondences(correspondences, cancellationToken);

        var initializedCorrespondences = new List<InitializedCorrespondences>();
        foreach (var correspondence in correspondences)
        {
            logger.LogInformation("Correspondence {correspondenceId} initialized", correspondence.Id);
            if (request.IdempotentKey.HasValue)
            {
                logger.LogInformation("Creating new idempotency key {Key}", request.IdempotentKey.Value);
                var idempotencyKey = new IdempotencyKeyEntity()
                {
                    Id = request.IdempotentKey.Value,
                    CorrespondenceId = correspondence.Id,
                    AttachmentId = null,
                    StatusAction = null,
                    IdempotencyType = IdempotencyType.Correspondence
                };
                try
                {
                    await idempotencyKeyRepository.CreateAsync(idempotencyKey, cancellationToken);
                }
                catch (DbUpdateException e)
                {
                    if (e.IsPostgresUniqueViolation()) // PostgreSQL unique constraint violation
                    {
                        logger.LogWarning("Idempotency key {Key} already exists in database", request.IdempotentKey.Value);
                        return CorrespondenceErrors.DuplicateInitCorrespondenceRequest;
                    }
                    throw;
                }
            }
            
            string? notificationJobId = null;
            var isReserved = correspondence.GetHighestStatus().Status == CorrespondenceStatus.Reserved;
            if (!isReserved)
            {
                if (correspondence.DueDateTime is not null)
                {
                    backgroundJobClient.Schedule<CorrespondenceDueDateHandler>(HangfireQueues.Default, (handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.DueDateTime.Value);
                }
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            
                if (request.Notification != null)
                {
                    notificationJobId = backgroundJobClient.Enqueue<CreateNotificationOrderHandler>((handler) => handler.Process(new CreateNotificationOrderRequest()
                    {
                        CorrespondenceId = correspondence.Id,
                        NotificationRequest = request.Notification,
                        Language = correspondence.Content.Language,
                    }, cancellationToken));
                }
            }

            foreach (var correspondenceAttachment in correspondence.Content.Attachments)
            {
                if (correspondenceAttachment.ExpirationTime is not DateTimeOffset scheduleAt)
                {
                    continue;
                }

                if (scheduleAt < DateTimeOffset.UtcNow)
                {
                    scheduleAt = DateTimeOffset.UtcNow;
                }

                backgroundJobClient.Schedule<ExpireAttachmentHandler>(
                    HangfireQueues.Default, (handler) => handler.Process(correspondenceAttachment.AttachmentId, null, CancellationToken.None),
                    scheduleAt);
            }

            var createJobResult = await CreateDialogOrTransmissionJob(correspondence, request, notificationJobId, cancellationToken);
            if (createJobResult.IsT1)
            {
                return createJobResult.AsT1;
            }

            if (correspondence.IsConfidential)
            {
                logger.LogInformation("Scheduling job to check for unread confidential correspondence for correspondence {CorrespondenceId}", correspondence.Id);
                var unreadCheckDelay = hostEnvironment.IsProduction()
                    ? correspondence.RequestedPublishTime.AddDays(7)
                    : correspondence.RequestedPublishTime.AddMinutes(1);
                backgroundJobClient.Schedule<UnreadConfidentialCorrespondenceHandler>(HangfireQueues.Default, (handler) => handler.Process(correspondence.Id, cancellationToken), unreadCheckDelay);
            }

            initializedCorrespondences.Add(new InitializedCorrespondences()
            {
                CorrespondenceId = correspondence.Id,
                Status = correspondence.GetHighestStatus().Status,
                Recipient = correspondence.Recipient
            });
        }

        return new InitializeCorrespondencesResponse()
        {
            Correspondences = initializedCorrespondences,
            AttachmentIds = correspondences.SelectMany(c => c.Content.Attachments.Select(a => a.AttachmentId)).Distinct().ToList()
        };
    }

    public async Task CreateDialogportenDialog(Guid correspondenceId)
    {
        logger.LogInformation("Creating Dialogporten dialog for correspondence {CorrespondenceId}", correspondenceId);
        var dialogId = await dialogportenService.CreateCorrespondenceDialog(correspondenceId);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
        logger.LogInformation("Successfully created Dialogporten dialog for correspondence {CorrespondenceId}", correspondenceId);
    }

    public async Task CreateDialogportenTransmission(Guid correspondenceId)
    {
        logger.LogInformation("Creating Dialogporten transmission for correspondence {CorrespondenceId}", correspondenceId);
        var transmissionId = await dialogportenService.CreateDialogTransmission(correspondenceId);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenTransmissionId, transmissionId);
        logger.LogInformation("Successfully created Dialogporten transmission for correspondence {CorrespondenceId}", correspondenceId);
    }

    private async Task<OneOf<Task, Error>> CreateDialogOrTransmissionJob(CorrespondenceEntity correspondence, InitializeCorrespondencesRequest request, string? notificationJobId, CancellationToken cancellationToken)
    {
        
        bool hasDialogId = correspondence.ExternalReferences.Any(er => er.ReferenceType == ReferenceType.DialogportenDialogId);
        if (hasDialogId)
        {
            var validationResult = await initializeCorrespondenceValidationHelper.ValidateTransmissionRequest(correspondence, request, cancellationToken);
            if (validationResult.IsT1)
            {
                return validationResult.AsT1;
            }

            logger.LogInformation("Correspondence {correspondenceId} already has a Dialogporten dialog, creating a transmission", correspondence.Id);

            if (!string.IsNullOrEmpty(notificationJobId))
            {
                #pragma warning disable CS4014 // Hangfire handles Task-returning job expressions by awaiting them during job execution
                backgroundJobClient.ContinueJobWith<InitializeCorrespondencesHandler>(notificationJobId, (handler) => handler.ScheduleTransmissionAndPublishJobs(correspondence.Id, request.Correspondence.Content!.Attachments.Count, correspondence.RequestedPublishTime, cancellationToken), JobContinuationOptions.OnAnyFinishedState);
                #pragma warning restore CS4014
            }
            else
            {
                backgroundJobClient.Enqueue<InitializeCorrespondencesHandler>((handler) => handler.ScheduleTransmissionAndPublishJobs(correspondence.Id, request.Correspondence.Content!.Attachments.Count, correspondence.RequestedPublishTime, cancellationToken));
            }
        }
        else
        {
            logger.LogInformation("Correspondence {correspondenceId} initialized", correspondence.Id);
            #pragma warning disable CS4014 // Hangfire handles Task-returning job expressions by awaiting them during job execution
            string dialogJob = !string.IsNullOrEmpty(notificationJobId)
                ? backgroundJobClient.ContinueJobWith(notificationJobId, () => CreateDialogportenDialog(correspondence.Id), JobContinuationOptions.OnAnyFinishedState)
                : backgroundJobClient.Enqueue(() => CreateDialogportenDialog(correspondence.Id));
            #pragma warning restore CS4014
            await hybridCacheWrapper.SetAsync($"dialogJobId_{correspondence.Id}", dialogJob, new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24)
            });
            backgroundJobClient.Enqueue<HangfireScheduleHelper>((helper) => helper.SchedulePublishAfterDialogCreated(correspondence.Id, cancellationToken));
        }

        return Task.CompletedTask;
    }

    public async Task ScheduleTransmissionAndPublishJobs(Guid correspondenceId, int attachmentsCount, DateTimeOffset requestedPublishTime, CancellationToken cancellationToken)
    {
        var scheduleAt = requestedPublishTime < DateTimeOffset.UtcNow
            ? DateTimeOffset.UtcNow
            : requestedPublishTime;
        var transmissionJob = backgroundJobClient.Schedule(() => CreateDialogportenTransmission(correspondenceId), scheduleAt);
        if (await correspondenceRepository.AreAllAttachmentsPublished(correspondenceId, cancellationToken))
        {
            await hangfireScheduleHelper.SchedulePublishAfterTransmissionCreated(correspondenceId, transmissionJob, cancellationToken);
        };
    }
}
