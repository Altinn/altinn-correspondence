using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;

public class PurgeCorrespondenceHandler : IHandler<PurgeCorrespondenceRequest, Guid>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly PurgeCorrespondenceHelper _purgeCorrespondenceHelper;

    public PurgeCorrespondenceHandler(
        IAltinnAuthorizationService altinnAuthorizationService,
        ICorrespondenceRepository correspondenceRepository,
        ICorrespondenceStatusRepository correspondenceStatusRepository,
        IEventBus eventBus,
        UserClaimsHelper userClaimsHelper,
        PurgeCorrespondenceHelper purgeCorrespondenceHelper)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _eventBus = eventBus;
        _userClaimsHelper = userClaimsHelper;
        _purgeCorrespondenceHelper = purgeCorrespondenceHelper;
    }

    public async Task<OneOf<Guid, Error>> Process(PurgeCorrespondenceRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        Guid correspondenceId = request.CorrespondenceId;
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        string? onBehalfOf = request.OnBehalfOf;
        bool isOnBehalfOfRecipient = false;
        bool isOnBehalfOfSender = false;
        if (!string.IsNullOrEmpty(onBehalfOf))
        {
            isOnBehalfOfRecipient = correspondence.Recipient.GetOrgNumberWithoutPrefix() == onBehalfOf.GetOrgNumberWithoutPrefix();
            isOnBehalfOfSender = correspondence.Sender.GetOrgNumberWithoutPrefix() == onBehalfOf.GetOrgNumberWithoutPrefix();
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            request.OnBehalfOf ?? correspondence.Recipient,
            correspondence.Id.ToString(),
            [ResourceAccessLevel.Read, ResourceAccessLevel.Write],
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        bool isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient) || isOnBehalfOfRecipient;
        bool isSender = _userClaimsHelper.IsSender(correspondence.Sender) || isOnBehalfOfSender;

        if (!isRecipient && !isSender)
        {
            return Errors.CorrespondenceNotFound;
        }
        var currentStatusError = _purgeCorrespondenceHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        if (isSender)
        {
            var senderRecipientPurgeError = _purgeCorrespondenceHelper.ValidatePurgeRequestSender(correspondence);
            if (senderRecipientPurgeError is not null)
            {
                return senderRecipientPurgeError;
            }
            await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByAltinn,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByAltinn.ToString()
            }, cancellationToken);
        }
        else if (isRecipient)
        {
            var recipientPurgeError = _purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
            if (recipientPurgeError is not null)
            {
                return recipientPurgeError;
            }
            await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
            {
                CorrespondenceId = correspondenceId,
                Status = CorrespondenceStatus.PurgedByRecipient,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = CorrespondenceStatus.PurgedByRecipient.ToString()
            }, cancellationToken);
        }

        await _eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        await _purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondenceId, cancellationToken);
        _purgeCorrespondenceHelper.ReportActivityToDialogporten(isSender, correspondenceId);
        _purgeCorrespondenceHelper.CancelNotification(correspondenceId, cancellationToken);
        return correspondenceId;
    }
}