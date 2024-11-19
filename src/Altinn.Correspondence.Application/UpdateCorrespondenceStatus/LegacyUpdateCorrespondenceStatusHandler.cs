using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
public class LegacyUpdateCorrespondenceStatusHandler : IHandler<UpdateCorrespondenceStatusRequest, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly IEventBus _eventBus;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly UpdateCorrespondenceStatusHelper _updateCorrespondenceStatusHelper;
    public LegacyUpdateCorrespondenceStatusHandler(
        ICorrespondenceRepository correspondenceRepository,
        ICorrespondenceStatusRepository correspondenceStatusRepository,
        IAltinnAuthorizationService altinnAuthorizationService,
        IAltinnRegisterService altinnRegisterService,
        IEventBus eventBus,
        UserClaimsHelper userClaimsHelper,
        UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper)
    {
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnRegisterService = altinnRegisterService;
        _eventBus = eventBus;
        _userClaimsHelper = userClaimsHelper;
        _updateCorrespondenceStatusHelper = updateCorrespondenceStatusHelper;
    }
    public async Task<OneOf<Guid, Error>> Process(UpdateCorrespondenceStatusRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
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
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, party.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel == null)
        {
            return Errors.LegacyNoAccessToCorrespondence;
        }
        var currentStatusError = _updateCorrespondenceStatusHelper.ValidateCurrentStatus(correspondence);
        if (currentStatusError is not null)
        {
            return currentStatusError;
        }
        var updateError = _updateCorrespondenceStatusHelper.ValidateUpdateRequest(request, correspondence);
        if (updateError is not null)
        {
            return updateError;
        }

        await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
        {
            CorrespondenceId = correspondence.Id,
            Status = request.Status,
            StatusChanged = DateTime.UtcNow,
            StatusText = request.Status.ToString(),
        }, cancellationToken);

        _updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, request.Status);
        await _updateCorrespondenceStatusHelper.PublishEvent(_eventBus, correspondence, request.Status, cancellationToken);
        return request.CorrespondenceId;
    }
}