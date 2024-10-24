using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.CheckNotification;

public class CheckNotificationHandler : IHandler<Guid, CheckNotificationResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    public CheckNotificationHandler(ICorrespondenceRepository correspondenceRepository)
    {
        _correspondenceRepository = correspondenceRepository;
    }

    public async Task<OneOf<CheckNotificationResponse, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        var response = new CheckNotificationResponse
        {
            SendNotification = true
        };
        if (correspondence == null)
        {
            response.SendNotification = false;
            return response;
        }
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Read))
        {
            response.SendNotification = false;
        }
        return response;
    }
}
