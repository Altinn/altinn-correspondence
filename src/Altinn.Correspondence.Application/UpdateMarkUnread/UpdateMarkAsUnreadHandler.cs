using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using OneOf;

namespace Altinn.Correspondence.Application.UpdateMarkAsUnread;

public class UpdateMarkAsUnreadHandler : IHandler<Guid, Guid>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IDialogportenService _dialogportenService;
    private readonly UserClaimsHelper _userClaimsHelper;
    public UpdateMarkAsUnreadHandler(ICorrespondenceRepository correspondenceRepository, IDialogportenService dialogportenService, UserClaimsHelper userClaimsHelper)
    {
        _correspondenceRepository = correspondenceRepository;
        _dialogportenService = dialogportenService;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<Guid, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient);
        if (!isRecipient)
        {
            return Errors.CorrespondenceNotFound;
        }

        var currentStatus = correspondence.GetLatestStatus();
        if (!correspondence.StatusHasBeen(CorrespondenceStatus.Read))
        {
            return Errors.CorrespondenceHasNotBeenRead;
        }
        if (currentStatus is null)
        {
            return Errors.LatestStatusIsNull;
        }
        if (currentStatus!.Status.IsPurged())
        {
            return Errors.CorrespondencePurged;
        }

        await _correspondenceRepository.UpdateMarkedUnread(correspondenceId, true, cancellationToken);
        return correspondenceId;
    }
}
