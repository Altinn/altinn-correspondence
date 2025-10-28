using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MigrateCorrespondenceHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceDeleteEventRepository correspondenceDeleteEventRepository,
    IDialogportenService dialogportenService,
    HangfireScheduleHelper hangfireScheduleHelper,
    IBackgroundJobClient backgroundJobClient,
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
            string dialogId = "";
            if (request.MakeAvailable)
            {
                var makeAvailableJob = backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.LiveMigration, (handler) => handler.MakeCorrespondenceAvailableInDialogportenAndApi(correspondence.Id, CancellationToken.None, null, true));
                backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(makeAvailableJob, HangfireQueues.LiveMigration, (helper) => helper.SchedulePublishAtPublishTime(correspondence.Id, CancellationToken.None));
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
            var sqlState = e.InnerException?.Data["SqlState"]?.ToString();
            if (sqlState == "23505")
            {
                var correspondence = await correspondenceRepository.GetCorrespondenceByAltinn2Id((int)request.CorrespondenceEntity.Altinn2CorrespondenceId, cancellationToken);
                return new MigrateCorrespondenceResponse()
                {
                    Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
                    CorrespondenceId = correspondence.Id,
                    IsAlreadyMigrated = true,
                    AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized }).ToList() ?? null
                };
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
        else if (request.BatchSize is not null)
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
            var batchLimit = 10000;
            if (request.BatchSize > batchLimit)
            {
                var currentBatch = batchLimit;
                var migrateRequest = new MakeCorrespondenceAvailableRequest()
                {
                    AsyncProcessing = true,
                    BatchSize = currentBatch,
                    CreateEvents = request.CreateEvents,
                    CursorCreated = request.CursorCreated,
                    CursorId = request.CursorId,
                    CreatedFrom = request.CreatedFrom,
                    CreatedTo = request.CreatedTo
                };
                backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.Migration, (handler) => handler.MakeCorrespondenceAvailable(migrateRequest, CancellationToken.None));
            } 
            else
            {
                var correspondences = await correspondenceRepository.GetCandidatesForMigrationToDialogporten(request.BatchSize ?? 0, request.CursorCreated, request.CursorId, request.CreatedFrom, request.CreatedTo, cancellationToken);
                // If we filled the window, continue with next cursor
                foreach (var correspondence in correspondences)
                {
                    backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.Migration, handler => handler.MakeCorrespondenceAvailableInDialogportenAndApi(correspondence.Id, CancellationToken.None, null, request.CreateEvents));
                }
                if (correspondences.Count == (request.BatchSize ?? 0) && correspondences.Count > 0)
                {
                    var last = correspondences.Last();
                    var migrateRequest = new MakeCorrespondenceAvailableRequest()
                    {
                        AsyncProcessing = true,
                        BatchSize = request.BatchSize,
                        CreateEvents = request.CreateEvents,
                        CursorCreated = last.Created,
                        CursorId = last.Id,
                        CreatedFrom = request.CreatedFrom,
                        CreatedTo = request.CreatedTo
                    };
                    backgroundJobClient.Enqueue<MigrateCorrespondenceHandler>(HangfireQueues.Migration, (handler) => handler.MakeCorrespondenceAvailable(migrateRequest, CancellationToken.None));
                }
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
        bool isSoftDeleted = await IsCorrespondenceSoftDeleted(correspondence, cancellationToken);
        var dialogId = await dialogportenService.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId: correspondenceId, correspondence: correspondence, enableEvents: createEvents, isSoftDeleted: isSoftDeleted);

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

    private async Task<bool> IsCorrespondenceSoftDeleted(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        var deletionEventsInDatabase = await correspondenceDeleteEventRepository.GetDeleteEventsForCorrespondenceId(correspondence.Id, cancellationToken);
        if (deletionEventsInDatabase == null || !deletionEventsInDatabase.Any())
        {
            return false;
        }

        var softDeletedEvent = deletionEventsInDatabase
            .Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient)
            .OrderByDescending(e => e.EventOccurred)
            .FirstOrDefault();

        var restoredEvent = deletionEventsInDatabase
            .Where(e => e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient)
            .OrderByDescending(e => e.EventOccurred)
            .FirstOrDefault();

        // If no soft delete event exists, it's not soft deleted
        if (softDeletedEvent == null)
        {
            return false;
        }

        // If no restore event exists, it is still soft deleted
        if (restoredEvent == null)
        {
            return true;
        }

        // Return true if soft delete occurred after the most recent restore
        return softDeletedEvent.EventOccurred > restoredEvent.EventOccurred;
    }
}
