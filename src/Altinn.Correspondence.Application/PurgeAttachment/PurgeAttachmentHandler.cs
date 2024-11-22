﻿using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.PurgeAttachment;

public class PurgeAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IStorageRepository storageRepository,
    ICorrespondenceRepository correspondenceRepository,
    IEventBus eventBus,
    UserClaimsHelper userClaimsHelper,
    ILogger<PurgeAttachmentHandler> logger) : IHandler<Guid, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IEventBus _eventBus = eventBus;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;
    private readonly ILogger<PurgeAttachmentHandler> _logger = logger;

    public async Task<OneOf<Guid, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        if (!_userClaimsHelper.IsSender(attachment.Sender))
        {
            return Errors.InvalidSender;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (attachment.StatusHasBeen(AttachmentStatus.Purged))
        {
            return Errors.InvalidPurgeAttachmentStatus;
        }

        var correspondences = await _correspondenceRepository.GetCorrespondencesByAttachmentId(attachmentId, true, cancellationToken);
        bool allCorrespondencesArePurged = correspondences
            .All(correspondence =>
            {
                var latestStatus = correspondence.GetLatestStatus();
                if (latestStatus is null) return false;
                return latestStatus.Status.IsPurged();
            });
        if (correspondences.Count != 0 && !allCorrespondencesArePurged)
        {
            return Errors.PurgeAttachmentWithExistingCorrespondence;
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            await _storageRepository.PurgeAttachment(attachmentId, cancellationToken);
            await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachmentId,
                Status = AttachmentStatus.Purged,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Purged.ToString()
            }, cancellationToken);

            await _eventBus.Publish(AltinnEventType.AttachmentPurged, attachment.ResourceId, attachmentId.ToString(), "attachment", attachment.Sender, cancellationToken);

            return attachmentId;
        }, _logger, cancellationToken);
    }
}
