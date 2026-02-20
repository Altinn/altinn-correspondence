using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence.Helpers;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MigrateCorrespondenceHandler(
ICorrespondenceRepository correspondenceRepository,
IDialogportenService dialogportenService,
HangfireScheduleHelper hangfireScheduleHelper,
IBackgroundJobClient backgroundJobClient,
IHostEnvironment hostEnvironment,
CorrespondenceMigrationEventHelper correspondenceMigrationEventHelper,
ILogger<MigrateCorrespondenceHandler> logger) : IHandler<MigrateCorrespondenceRequest, MigrateCorrespondenceResponse>
{
    public async Task<OneOf<MigrateCorrespondenceResponse, Error>> Process(MigrateCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var contentError = MigrationValidateCorrespondenceContent(request.CorrespondenceEntity.Content);
        if (contentError != null)
        {
            return contentError;
        }

        if (request.CorrespondenceEntity?.Content?.Attachments != null && request?.ExistingAttachments != null)
        {
            request.CorrespondenceEntity.Content.Attachments.AddRange
            (
                request.ExistingAttachments.Select(a => new CorrespondenceAttachmentEntity()
                {
                    AttachmentId = a,
                    Created = request.CorrespondenceEntity.Created
                })
            );
        }

        try
        {
            var correspondence = await correspondenceRepository.CreateCorrespondence(request.CorrespondenceEntity, cancellationToken);
            
            if (request.DeleteEventEntities != null && request.DeleteEventEntities.Any()) // Handled separately
            {
                foreach (var deleteEvent in request.DeleteEventEntities)
                {
                    await correspondenceMigrationEventHelper.StoreDeleteEventForCorrespondence(correspondence, deleteEvent, DateTimeOffset.UtcNow, cancellationToken);
                }
            }

            string dialogId = "";
            if (request.MakeAvailable)
            {
                if (hostEnvironment.IsDevelopment())
                {
                    try { 
                        dialogId = await MakeCorrespondenceAvailableInDialogportenAndApi(correspondence.Id, cancellationToken, correspondence, true);
                    } catch (Exception ex)
                    {
                        // Used for tests
                    }
                }
                else
                {
                    var dialogJobId = backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.LiveMigration, (handler) => handler.MakeCorrespondenceAvailableInDialogportenAndApi(correspondence.Id, CancellationToken.None, null, true));
                    hangfireScheduleHelper.SchedulePublishAfterDialogCreated(correspondence.Id, dialogJobId, CancellationToken.None);

                    var altinn2PublishStatus = correspondence.Statuses.FirstOrDefault(statusEvent => statusEvent.Status == CorrespondenceStatus.Published && statusEvent.StatusText == "Correspondence Published in Altinn 2");
                    if (altinn2PublishStatus != null)
                    {
                        logger.LogInformation("Correspondence {CorrespondenceId} was previously published in Altinn 2 at {PublishedAt}", correspondence.Id, altinn2PublishStatus.StatusChanged);
                        await correspondenceRepository.UpdatePublished(correspondence.Id, altinn2PublishStatus.StatusChanged, cancellationToken);
                        backgroundJobClient.Enqueue<ProcessLegacyPartyHandler>((handler) => handler.Process(correspondence!.Recipient, null, CancellationToken.None));
                    }
                }
            }
            
            return new MigrateCorrespondenceResponse()
            {
                Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
                CorrespondenceId = correspondence.Id,
                AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized }).ToList() ?? null,
                DialogId = request.MakeAvailable ? dialogId : null
            };
        }
        catch (DbUpdateException e)
        {
            if (e.IsPostgresUniqueViolation()) // Correspondence Already Migrated
            {
                // Use transaction protection for remigration logic
                var remigrationResult = await TransactionWithRetriesPolicy.Execute<MigrateCorrespondenceResponse>(async (cancellationToken) =>
                {
                    // Fetch correspondence within transaction to get fresh data
                    var existingCorrespondence = await correspondenceRepository.GetCorrespondenceByAltinn2Id((int)request.CorrespondenceEntity.Altinn2CorrespondenceId, cancellationToken);
                    if (existingCorrespondence == null)
                    {
                        throw new InvalidOperationException($"Correspondence with Altinn2Id {request.CorrespondenceEntity.Altinn2CorrespondenceId} not found during remigration transaction");
                    }

                    var correspondenceId = existingCorrespondence.Id;

                    // Clear the change tracker to prevent EF Core from tracking request.CorrespondenceEntity
                    // when processing events that have navigation properties pointing to it
                    correspondenceRepository.ClearChangeTracker();

                    // Process all event types if they exist in the request
                    var eventsProcessed = await correspondenceMigrationEventHelper.ProcessAllEventsForCorrespondence(
                        correspondenceId,
                        existingCorrespondence,
                        request.CorrespondenceEntity.Statuses,
                        request.DeleteEventEntities,
                        request.CorrespondenceEntity.Notifications,
                        request.CorrespondenceEntity.ForwardingEvents,
                        MigrationOperationType.Remigrate,
                        cancellationToken);

                    if (eventsProcessed == 0)
                    {
                        logger.LogInformation("No new events to remigrate for Correspondence {CorrespondenceId}. Exiting remigrate process.", correspondenceId);
                    }

                    return new MigrateCorrespondenceResponse()
                    {
                        Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
                        CorrespondenceId = correspondenceId,
                        IsAlreadyMigrated = true,
                        AttachmentMigrationStatuses = existingCorrespondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized }).ToList() ?? null
                    };
                }, logger, cancellationToken);

                return remigrationResult;
            }

            throw;
        }
    }

    public async Task<OneOf<MakeCorrespondenceAvailableResponse, Error>> MakeCorrespondenceAvailable(MakeCorrespondenceAvailableRequest request, CancellationToken cancellationToken)
    {
        string? dialogId;
        MakeCorrespondenceAvailableResponse response = new MakeCorrespondenceAvailableResponse()
        {
            Statuses = new ()
        };
        if (request.CorrespondenceId.HasValue)
        {
            try
            {
                dialogId = await MakeCorrespondenceAvailableInDialogportenAndApi(request.CorrespondenceId.Value, cancellationToken);
                response.Statuses.Add(new(request.CorrespondenceId.Value, null, dialogId, true));
            }
            catch (Exception ex)
            {
                response.Statuses.Add(new(request.CorrespondenceId.Value, ex.ToString()));
            }
        }
        else if (request.CorrespondenceIds != null && request.CorrespondenceIds.Any())
        {
            foreach (var cid in request.CorrespondenceIds)
            {
                try
                {
                    dialogId = await MakeCorrespondenceAvailableInDialogportenAndApi(cid, cancellationToken);
                    response.Statuses.Add(new(cid, null, dialogId, true));
                }
                catch (Exception ex)
                {
                    response.Statuses.Add(new(cid, ex.ToString()));
                }
            }
        }
        else if (request.BatchSize is not null && request.BatchSize > 0)
        {
            if (!request.AsyncProcessing)
            {
                var correspondences = await correspondenceRepository.GetCandidatesForMigrationToDialogporten(request.BatchSize ?? 0, request.CursorCreated, request.CursorId, request.CreatedFrom, request.CreatedTo, cancellationToken);
                foreach (var correspondence in correspondences)
                {
                    try
                    {
                        dialogId = await MakeCorrespondenceAvailableInDialogportenAndApi(correspondence.Id, cancellationToken, null, request.CreateEvents);
                        response.Statuses.Add(new(correspondence.Id, null, dialogId, true));
                    }
                    catch (Exception ex)
                    {
                        response.Statuses.Add(new(correspondence.Id, ex.ToString()));
                    }
                }
                return response;
            }
            var currentBatch = 999;

            var enqueuedJobs = JobStorage.Current.GetMonitoringApi().EnqueuedCount(HangfireQueues.Migration);
            if (enqueuedJobs > currentBatch * 20)
            {
                var migrateRequest = new MakeCorrespondenceAvailableRequest()
                {
                    AsyncProcessing = true,
                    BatchSize = request.BatchSize,
                    CreateEvents = request.CreateEvents,
                    CursorCreated = request.CursorCreated,
                    CursorId = request.CursorId,
                    CreatedFrom = request.CreatedFrom,
                    CreatedTo = request.CreatedTo
                };
                logger.LogInformation("Delaying scheduling of migration jobs as there are currently {EnqueuedJobs} jobs in the queue", enqueuedJobs);
                backgroundJobClient.Schedule<MigrateCorrespondenceHandler>(HangfireQueues.LiveMigration, (handler) => handler.MakeCorrespondenceAvailable(migrateRequest, CancellationToken.None), TimeSpan.FromMinutes(1));
            } 
            else
            {
                logger.LogInformation("Querying db");
                var correspondences = await correspondenceRepository.GetCandidatesForMigrationToDialogporten(currentBatch, request.CursorCreated, request.CursorId, request.CreatedFrom, request.CreatedTo, cancellationToken);
                logger.LogInformation("Found {count} correspondences", correspondences.Count);
                var last = correspondences.Last();
                var migrateRequest = new MakeCorrespondenceAvailableRequest()
                {
                    AsyncProcessing = true,
                    BatchSize = request.BatchSize - correspondences.Count,
                    CreateEvents = request.CreateEvents,
                    CursorCreated = last.Created,
                    CursorId = last.Id,
                    CreatedFrom = request.CreatedFrom,
                    CreatedTo = request.CreatedTo
                };
                logger.LogInformation("Enqueuing next batch for {id}", last.Id);
                backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.LiveMigration, (handler) => handler.MakeCorrespondenceAvailable(migrateRequest, CancellationToken.None));
                foreach (var correspondence in correspondences)
                {
                    backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.Migration, handler => handler.MakeCorrespondenceAvailableInDialogportenAndApi(correspondence.Id, CancellationToken.None, true, null, request.CreateEvents));
                }
                logger.LogInformation("Finished queuing {count} correspondences", correspondences.Count);
            }
        }

        return response;
    }

    public async Task<string> MakeCorrespondenceAvailableInDialogportenAndApi(Guid correspondenceId)
    {
        return await MakeCorrespondenceAvailableInDialogportenAndApi(correspondenceId, CancellationToken.None, null, false);
    }

    public async Task<string> MakeCorrespondenceAvailableInDialogportenAndApi(Guid correspondenceId, CancellationToken cancellationToken, CorrespondenceEntity? correspondenceEntity = null, bool createEvents = false)
    {
        return await MakeCorrespondenceAvailableInDialogportenAndApi(correspondenceId, cancellationToken, false, correspondenceEntity, createEvents);
    }

    public async Task<string> MakeCorrespondenceAvailableInDialogportenAndApi(Guid correspondenceId, CancellationToken cancellationToken, bool isPrepublished, CorrespondenceEntity ? correspondenceEntity = null, bool createEvents = false)
    {
        var correspondence = correspondenceEntity ?? await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken, true);
        if (correspondence == null)
        {
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }

        if (correspondence.ExternalReferences.Any(er => er.ReferenceType == ReferenceType.DialogportenDialogId))
        {
            logger.LogError($"Correspondence with id {correspondenceId} is already available in Dialogporten and API");
            return correspondence.ExternalReferences.First(er => er.ReferenceType == ReferenceType.DialogportenDialogId).ReferenceValue;
        }

        if (correspondence.HasBeenPurged())
        {
            throw new InvalidOperationException($"Correspondence with id {correspondenceId} is purged and cannot be made available in Dialogporten or API");
        }
        
        if(!string.IsNullOrEmpty(correspondence.Recipient) && correspondence.Recipient.IsWithPartyUuidPrefix())
        {
            throw new InvalidOperationException($"Correspondence with id {correspondenceId} has a Self-Identifed user as recipient and cannot be made available in Dialogporten");
        }

        // If the correspondence was soft deleted in Altinn 2, we need to pass this forward in order to set the system label correctly on the Dialog
        bool isSoftDeleted = await correspondenceMigrationEventHelper.IsCorrespondenceSoftDeleted(correspondence, cancellationToken);
        var dialogId = await dialogportenService.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId: correspondenceId, correspondence: correspondence, enableEvents: createEvents, isSoftDeleted: isSoftDeleted);
        if (string.IsNullOrEmpty(dialogId))
        {
            logger.LogError($"Dialogporten service failed to create a dialog for correspondence with id {correspondenceId}");
            return string.Empty;
        }
        var updateResult = await TransactionWithRetriesPolicy.Execute<string>(async (cancellationToken) =>
        {
            if (correspondence.ExternalReferences.Any(er => er.ReferenceType == ReferenceType.DialogportenDialogId))
            {
                logger.LogError($"Correspondence with id {correspondenceId} is already available in Dialogporten and API");
                return correspondence.ExternalReferences.First(er => er.ReferenceType == ReferenceType.DialogportenDialogId).ReferenceValue;
            }
            await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
            await SetIsMigrating(correspondenceId, false, cancellationToken);
            return dialogId;
        }, logger, cancellationToken);

        return dialogId;
    }

    /// <summary>
    /// This should only really be used when a Correspondence is being made available in Dialogporten and API, which means IsMigrating should always be false.
    /// However we are making it take a boolean in case we find it necessary to make Correspondences unavailable for some reason in the future.
    /// </summary>
    private async Task SetIsMigrating(Guid correspondenceId, bool isMigrating, CancellationToken cancellationToken)
    {
        await correspondenceRepository.UpdateIsMigrating(correspondenceId, isMigrating, cancellationToken);
    }

    public static Error? MigrationValidateCorrespondenceContent(CorrespondenceContentEntity? content)
    {
        if (content == null)
        {
            return CorrespondenceErrors.MissingContent;
        }

        if (string.IsNullOrWhiteSpace(content.MessageTitle))
        {
            return CorrespondenceErrors.MessageTitleEmpty;
        }

        if (!IsLanguageValid(content.Language))
        {
            return CorrespondenceErrors.InvalidLanguage;
        }

        return null;
    }
    private static bool IsLanguageValid(string language)
    {
        List<string> supportedLanguages = ["nb", "nn", "en"];
        return supportedLanguages.Contains(language.ToLower());
    }
}
