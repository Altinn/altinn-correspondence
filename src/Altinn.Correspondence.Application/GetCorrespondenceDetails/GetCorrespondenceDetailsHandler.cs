using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetails;

public class GetCorrespondenceDetailsHandler : IHandler<Guid, GetCorrespondenceDetailsResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;

    public GetCorrespondenceDetailsHandler(ICorrespondenceRepository correspondenceRepository)
    {
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<GetCorrespondenceDetailsResponse, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var correspondenceContent = await _correspondenceRepository.GetCorrespondenceContent(correspondenceId, cancellationToken);
        if (correspondenceContent is null)
        {
            throw new Exception("Invalid state");
        }
        var latestStatus = correspondence.Statuses.OrderByDescending(s => s.StatusChanged).First();
        var response = new GetCorrespondenceDetailsResponse
        {
            CorrespondenceId = correspondence.Id,
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText,
            StatusChanged = latestStatus.StatusChanged,
            SendersReference = correspondence.SendersReference,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            ReplyOptions = correspondence.ReplyOptions == null ? new List<CorrespondenceReplyOptionEntity>() : correspondence.ReplyOptions,
            Notifications = correspondence.Notifications == null ? new List<CorrespondenceNotificationEntity>() : correspondence.Notifications,
            VisibleFrom = correspondence.VisibleFrom,
            IsReservable = correspondence.IsReservable == null || correspondence.IsReservable.Value,
            StatusHistory = correspondence.Statuses,
            CorrespondenceContent = correspondenceContent
        };
        return response;
    }
}
