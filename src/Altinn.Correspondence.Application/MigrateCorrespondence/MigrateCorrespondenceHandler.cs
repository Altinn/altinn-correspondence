using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

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
            return new MigrateCorrespondenceResponse()
            {
                Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
                CorrespondenceId = correspondence.Id,
                AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized }).ToList() ?? null
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

    public async Task<OneOf<MakeAvailableInDialogportenResponse, Error>> MakeAvailableInDialogPorten(MakeAvailableInDialogportenRequest request, CancellationToken cancellationToken)
    {
        string? dialogId;
        MakeAvailableInDialogportenResponse response = new MakeAvailableInDialogportenResponse()
        {
            IsAlreadyMadeAvailable = false
        };
        if (request.CorrespondenceId.HasValue)
        {
            dialogId = await dialogportenService.SilentCreateCorrespondenceDialog(request.CorrespondenceId.Value);
            await correspondenceRepository.AddExternalReference(request.CorrespondenceId.Value, ReferenceType.DialogportenDialogId, dialogId);
            response.CorrespondenceId = request.CorrespondenceId;
        }
        else if (request.CorrespondenceIds != null && request.CorrespondenceIds.Any())
        {
            response.CorrespondenceIds = new List<Guid>();
            foreach (var cid in request.CorrespondenceIds)
            {
                dialogId = await dialogportenService.SilentCreateCorrespondenceDialog(cid);
                await correspondenceRepository.AddExternalReference(cid, ReferenceType.DialogportenDialogId, dialogId);
                response.CorrespondenceIds.Add(cid);
            }
        }

        return response;
    }

    public async Task CreateDialogportenDialog(Guid correspondenceId)
    {
        var dialogId = await dialogportenService.SilentCreateCorrespondenceDialog(correspondenceId);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
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
