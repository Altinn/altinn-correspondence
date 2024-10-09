﻿using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentHandler : IHandler<DownloadCorrespondenceAttachmentRequest, Stream>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IStorageRepository _storageRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IDialogportenService _dialogportenService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public DownloadCorrespondenceAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IStorageRepository storageRepository, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, IDialogportenService dialogportenService, UserClaimsHelper userClaimsHelper)
    {
        _correspondenceRepository = correspondenceRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _storageRepository = storageRepository;
        _attachmentRepository = attachmentRepository;
        _dialogportenService = dialogportenService;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<Stream, Error>> Process(DownloadCorrespondenceAttachmentRequest request, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var attachment = await _attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (!_userClaimsHelper.IsRecipient(correspondence.Recipient))
        {
            return Errors.CorrespondenceNotFound;
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            return Errors.CorrespondenceNotFound;
        }
        var attachmentStream = await _storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        await _dialogportenService.CreateInformationActivity(request.CorrespondenceId, Core.Services.Enums.DialogportenActorType.Recipient, $"Startet nedlastning av {attachment.FileName}", cancellationToken: cancellationToken);
        return attachmentStream;
    }
}
