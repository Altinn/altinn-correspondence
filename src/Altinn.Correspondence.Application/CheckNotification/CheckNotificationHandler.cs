using Altinn.Correspondence.Application.EnsureNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.CheckNotification;

public class CheckNotificationHandler(ICorrespondenceRepository correspondenceRepository, IBackgroundJobClient backgroundJobClient) : IHandler<Guid, CheckNotificationResponse>
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
        if (!correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            backgroundJobClient.Schedule<EnsureNotificationHandler>(handler => handler.Process(correspondenceId, null, CancellationToken.None), DateTimeOffset.Now.AddHours(1));
            response.SendNotification = false;
        }
        return response;
    }
}
