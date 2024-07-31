using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverview;

public class GetCorrespondenceOverviewHandler : IHandler<Guid, GetCorrespondenceOverviewResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly ICorrespondenceRepository _CorrespondenceRepository;

    public GetCorrespondenceOverviewHandler(IAltinnAuthorizationService altinnAuthorizationService, ICorrespondenceRepository CorrespondenceRepository)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _CorrespondenceRepository = CorrespondenceRepository;
    }

    public async Task<OneOf<GetCorrespondenceOverviewResponse, Error>> Process(Guid CorrespondenceId, CancellationToken cancellationToken)
    {

        var correspondence = await _CorrespondenceRepository.GetCorrespondenceById(CorrespondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var latestStatus = correspondence.Statuses?.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var response = new GetCorrespondenceOverviewResponse
        {
            CorrespondenceId = correspondence.Id,
            Content = correspondence.Content,
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText,
            StatusChanged = latestStatus.StatusChanged,
            SendersReference = correspondence.SendersReference,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            ReplyOptions = correspondence.ReplyOptions ?? new List<CorrespondenceReplyOptionEntity>(),
            Notifications = correspondence.Notifications ?? new List<CorrespondenceNotificationEntity>(),
            VisibleFrom = correspondence.VisibleFrom,
            IsReservable = correspondence.IsReservable == null || correspondence.IsReservable.Value,
            MarkedUnread = correspondence.MarkedUnread
        };
        return response;
    }
}
