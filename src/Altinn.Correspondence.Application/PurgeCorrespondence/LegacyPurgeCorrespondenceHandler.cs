using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using OneOf;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;
public class LegacyPurgeCorrespondenceHandler : IHandler<Guid, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly IEventBus _eventBus;
    private readonly PurgeCorrespondenceHelper _purgeCorrespondenceHelper;
    private readonly UserClaimsHelper _userClaimsHelper;
    public LegacyPurgeCorrespondenceHandler(
        ICorrespondenceRepository correspondenceRepository,
        ICorrespondenceStatusRepository correspondenceStatusRepository,
        IAltinnAuthorizationService altinnAuthorizationService,
        IAltinnRegisterService altinnRegisterService,
        IEventBus eventBus,
        PurgeCorrespondenceHelper purgeCorrespondenceHelper,
        UserClaimsHelper userClaimsHelper)
    {
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnRegisterService = altinnRegisterService;
        _eventBus = eventBus;
        _purgeCorrespondenceHelper = purgeCorrespondenceHelper;
        _userClaimsHelper = userClaimsHelper;
    }
    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        if (_userClaimsHelper.GetPartyId() is not int partyId)
        {
            return Errors.InvalidPartyId;
        }
        var party = await _altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (party is null || (string.IsNullOrEmpty(party.SSN) && string.IsNullOrEmpty(party.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(party.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return Errors.LegacyNoAccessToCorrespondence;
        }
        var currentStatusError = _purgeCorrespondenceHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        var recipientPurgeError = _purgeCorrespondenceHelper.ValidatePurgeRequestRecipient(correspondence);
        if (recipientPurgeError is not null)
        {
            return recipientPurgeError;
        }

        await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
        {
            CorrespondenceId = correspondenceId,
            Status = CorrespondenceStatus.PurgedByRecipient,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = CorrespondenceStatus.PurgedByRecipient.ToString()
        }, cancellationToken);

        await _eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondenceId.ToString(), "correspondence", correspondence.Sender, cancellationToken);
        await _purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondenceId, cancellationToken);
        _purgeCorrespondenceHelper.CreateInformationActivityDialogporten(isSender: false, correspondenceId);
        _purgeCorrespondenceHelper.CancelNotification(correspondenceId, cancellationToken);
        return correspondenceId;
    }
}