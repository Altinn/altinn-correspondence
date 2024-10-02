using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewHandler : IHandler<Guid, GetCorrespondenceOverviewResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _CorrespondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly UserClaimsHelper _userClaimsHelper;

    public GetCorrespondenceOverviewHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository CorrespondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, UserClaimsHelper userClaimsHelper)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _CorrespondenceRepository = CorrespondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<GetCorrespondenceOverviewResponse, Error>> Process(Guid CorrespondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _CorrespondenceRepository.GetCorrespondenceById(CorrespondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        /*var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }*/
        if (!_userClaimsHelper.IsAffiliatedWithCorrespondence(correspondence.Recipient, correspondence.Sender))
        {
            return Errors.CorrespondenceNotFound;
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        if (_userClaimsHelper.IsRecipient(correspondence.Recipient))
        {
            if (!latestStatus.Status.IsAvailableForRecipient())
            {
                return Errors.CorrespondenceNotFound;
            }
            await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity
            {
                CorrespondenceId = correspondence.Id,
                Status = CorrespondenceStatus.Fetched,
                StatusText = CorrespondenceStatus.Fetched.ToString(),
                StatusChanged = DateTime.Now
            }, cancellationToken);
        }
        var response = new GetCorrespondenceOverviewResponse
        {
            CorrespondenceId = correspondence.Id,
            Content = correspondence.Content,
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText,
            StatusChanged = latestStatus.StatusChanged,
            ResourceId = correspondence.ResourceId,
            Sender = correspondence.Sender,
            SendersReference = correspondence.SendersReference,
            MessageSender = correspondence.MessageSender ?? string.Empty,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
            Notifications = correspondence.Notifications ?? new List<CorrespondenceNotificationEntity>(),
            ExternalReferences = correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>(),
            VisibleFrom = correspondence.VisibleFrom,
            IsReservable = correspondence.IsReservable == null || correspondence.IsReservable.Value,
            MarkedUnread = correspondence.MarkedUnread,
            AllowSystemDeleteAfter = correspondence.AllowSystemDeleteAfter,
        };
        return response;
    }
}
