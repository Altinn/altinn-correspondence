using Altinn.Correspondence.Application.GetCorrespondenceOverviewCommand;
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

        var Correspondence = await _CorrespondenceRepository.GetCorrespondenceById(CorrespondenceId, true, cancellationToken);
        var latestStatus = Correspondence?.Statuses.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if (Correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var response = new GetCorrespondenceOverviewCommandResponse
        {
            CorrespondenceId = Correspondence.Id,
            Status = latestStatus?.Status,
            StatusText = latestStatus?.StatusText,
            StatusChanged = latestStatus?.StatusChanged,
            SendersReference = Correspondence.SendersReference,
            Created = Correspondence.Created,
            Recipient = Correspondence.Recipient,
            ReplyOptions = Correspondence.ReplyOptions,
            Notifications = Correspondence.Notifications,
            VisibleFrom = Correspondence.VisibleFrom,
            IsReservable = Correspondence.IsReservable == null || Correspondence.IsReservable.Value,
        };
        return response;
    }


}
