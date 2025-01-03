using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.CheckNotification;

public class CheckNotificationHandler(ICorrespondenceRepository correspondenceRepository) : IHandler<Guid, CheckNotificationResponse>
{

    public async Task<OneOf<CheckNotificationResponse, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
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
