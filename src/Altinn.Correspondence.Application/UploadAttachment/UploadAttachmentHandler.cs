using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.Settings;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class UploadAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    AttachmentHelper attachmentHelper,
    ILogger<UploadAttachmentHandler> logger) : IHandler<UploadAttachmentRequest, UploadAttachmentResponse>
{
    public async Task<OneOf<UploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing upload for attachment {AttachmentId}", request.AttachmentId);
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            logger.LogError("Attachment with id {AttachmentId} not found", request.AttachmentId);
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for attachment {AttachmentId} - user does not have sender access", request.AttachmentId);
            return AuthorizationErrors.NoAccessToResource;
        }
        if (request.ContentLength is not null && (request.ContentLength > ApplicationConstants.MaxFileStreamUploadSize || request.ContentLength == 0))
        {
            logger.LogWarning("Invalid file size {ContentLength} for attachment {AttachmentId} - must be between 1 and {MaxSize} bytes",
                request.ContentLength, request.AttachmentId, ApplicationConstants.MaxFileStreamUploadSize);
            return AttachmentErrors.InvalidFileSize("5GB");
        }
        if (attachment.StatusHasBeen(AttachmentStatus.UploadProcessing))
        {
            logger.LogWarning("Attachment {AttachmentId} has already been uploaded or is being processed", request.AttachmentId);
            return AttachmentErrors.FileAlreadyUploaded;
        }
        var correspondences = await correspondenceRepository.GetCorrespondencesByAttachmentId(request.AttachmentId, false);
        if (correspondences.Count != 0)
        {
            logger.LogWarning("Cannot upload attachment {AttachmentId} - it is already linked to {Count} correspondences", 
                request.AttachmentId, correspondences.Count);
            return AttachmentErrors.CantUploadToExistingCorrespondence;
        }
        var caller = user?.GetCallerPartyUrn();
        var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for caller {caller}", caller);
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        logger.LogInformation("Retrieved party UUID {PartyUuid} for caller {caller}", partyUuid, caller);
        try
        {
            var uploadResponse = await attachmentHelper.UploadAttachment(request.UploadStream, request.AttachmentId, partyUuid, cancellationToken);
            if (!uploadResponse.TryPickT0(out var uploadAttachmentResponse, out var error))
            {
                logger.LogError("Failed to upload attachment {AttachmentId}: {ErrorMessage}", request.AttachmentId, error.Message);
                return error;
            }
            logger.LogInformation("Successfully uploaded attachment {AttachmentId}", request.AttachmentId);
            return uploadResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading attachment {AttachmentId}", request.AttachmentId);
            throw;
        }
    }
}
