using Altinn.Correspondence.Application.GetCorrespondenceDetailsCommand;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceDetailsCommand;

public class GetCorrespondenceDetailsCommandHandler : IHandler<Guid, GetCorrespondenceDetailsCommandResponse>
{
    private readonly ICorrespondenceRepository _CorrespondenceRepository;
    public GetCorrespondenceDetailsCommandHandler(ICorrespondenceRepository CorrespondenceRepository)
    {
        _CorrespondenceRepository = CorrespondenceRepository;
    }

    public async Task<OneOf<GetCorrespondenceDetailsCommandResponse, Error>> Process(Guid CorrespondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _CorrespondenceRepository.GetCorrespondenceById(CorrespondenceId, true, cancellationToken);
        var latestStatus = correspondence?.Statuses.OrderByDescending(s => s.StatusChanged).FirstOrDefault();
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var response = new GetCorrespondenceDetailsCommandResponse
        {
            CorrespondenceId = correspondence.Id,
            Status = latestStatus?.Status,
            StatusText = latestStatus?.StatusText,
            StatusChanged = latestStatus?.StatusChanged,
            SendersReference = correspondence.SendersReference,
            Created = correspondence.Created,
            Recipient = correspondence.Recipient,
            ReplyOptions = correspondence.ReplyOptions,
            Notifications = correspondence.Notifications,
            VisibleFrom = correspondence.VisibleFrom,
            IsReservable = correspondence.IsReservable == null || correspondence.IsReservable.Value,
            StatusHistory = correspondence.Statuses
        };
        return response;
    }


}
