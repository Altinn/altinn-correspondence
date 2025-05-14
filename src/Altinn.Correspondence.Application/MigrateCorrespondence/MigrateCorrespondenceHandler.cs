using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.InitializeCorrespondence;

public class MigrateCorrespondenceHandler(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    ICorrespondenceRepository correspondenceRepository,
    ILogger<MigrateCorrespondenceHandler> logger) : IHandler<MigrateCorrespondenceRequest, MigrateCorrespondenceResponse>
{
    public async Task<OneOf<MigrateCorrespondenceResponse, Error>> Process(MigrateCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var contentError = MigrationValidateCorrespondenceContent(request.CorrespondenceEntity.Content);
        if (contentError != null)
        {
            return contentError;
        }

        return await TransactionWithRetriesPolicy.Execute<MigrateCorrespondenceResponse>(async (cancellationToken) =>
        {
            if (request.CorrespondenceEntity?.Content?.Attachments != null)
            {
                request.CorrespondenceEntity.Content.Attachments.AddRange
                (
                    request.ExistingAttachments.Select(a => new CorrespondenceAttachmentEntity()
                    {
                        AttachmentId = a,
                        Created = DateTimeOffset.UtcNow
                    })
                );
            }

            var correspondence = await correspondenceRepository.CreateCorrespondence(request.CorrespondenceEntity, cancellationToken);
            return new MigrateCorrespondenceResponse()
            {
                Altinn2CorrespondenceId = request.Altinn2CorrespondenceId,
                CorrespondenceId = correspondence.Id,
                AttachmentMigrationStatuses = correspondence.Content?.Attachments.Select(a => new AttachmentMigrationStatus() { AttachmentId = a.AttachmentId, AttachmentStatus = AttachmentStatus.Initialized }).ToList() ?? null
            };
        }, logger, cancellationToken);

    }

    public Error? MigrationValidateCorrespondenceContent(CorrespondenceContentEntity? content)
    {
        if (content == null)
        {
            return CorrespondenceErrors.MissingContent;
        }
        if (string.IsNullOrWhiteSpace(content.MessageTitle))
        {
            return CorrespondenceErrors.MessageTitleEmpty;
        }
        if (!TextValidation.ValidatePlainText(content.MessageTitle))
        {
            return CorrespondenceErrors.MessageTitleIsNotPlainText;
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
