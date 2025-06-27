using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.MigrateCorrespondence;

public class MigrateCorrespondenceHandler(
    ICorrespondenceRepository correspondenceRepository,
    IDialogportenService dialogportenService,
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
                dialogId = await CreateDialogportenDialog(correspondence.Id, cancellationToken, correspondence, true);
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

    public async Task<OneOf<MakeCorrespondenceAvailableResponse, Error>> MakeAvailableInDialogPorten(MakeCorrespondenceAvailableRequest request, CancellationToken cancellationToken)
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
                dialogId = await CreateDialogportenDialog(request.CorrespondenceId.Value, cancellationToken);
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
                    dialogId = await CreateDialogportenDialog(cid, cancellationToken);
                    response.Statuses.Add(new(cid, null, dialogId, true));
                }
                catch (Exception ex)
                {
                    response.Statuses.Add(new(cid, ex.ToString()));
                }
            }
        }

        return response;
    }

    private async Task<string> CreateDialogportenDialog(Guid correspondenceId, CancellationToken cancellationToken, CorrespondenceEntity? correspondenceEntity = null, bool createEvents = false)
    {
        var correspondence = correspondenceEntity ?? await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        if (correspondence == null)
        {
            throw new ArgumentException($"Correspondence with id {correspondenceId} not found", nameof(correspondenceId));
        }
        var dialogId = await dialogportenService.CreateCorrespondenceDialogForMigratedCorrespondence(correspondenceId, correspondence, createEvents);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
        correspondence.ExternalReferences.Add(new ExternalReferenceEntity() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = dialogId });
        await SetIsMigrating(correspondenceId, false, cancellationToken);
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
