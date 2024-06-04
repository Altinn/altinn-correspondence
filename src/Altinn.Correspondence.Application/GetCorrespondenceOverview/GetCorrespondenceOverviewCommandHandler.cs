using Altinn.Correspondence.Application.GetCorrespondenceOverviewCommand;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceOverviewCommand;

public class GetCorrespondenceOverviewCommandHandler : IHandler<Guid, GetCorrespondenceOverviewCommandResponse>
{
    private readonly ICorrespondenceRepository _CorrespondenceRepository;
    public GetCorrespondenceOverviewCommandHandler(ICorrespondenceRepository CorrespondenceRepository)
    {
        _CorrespondenceRepository = CorrespondenceRepository;
    }

    public async Task<OneOf<GetCorrespondenceOverviewCommandResponse, Error>> Process(Guid CorrespondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _CorrespondenceRepository.GetCorrespondenceById(CorrespondenceId, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var latestStatus = correspondence.Statuses?.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var response = new GetCorrespondenceOverviewCommandResponse
        {
            CorrespondenceId = correspondence.Id,
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
        };
        return response;
    }
}
