using Altinn.Correspondence.Application.EnsureNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.CheckNotification;

public class CheckNotificationHandler(
    ICorrespondenceRepository correspondenceRepository, 
    IBackgroundJobClient backgroundJobClient,
    ILogger<CheckNotificationHandler> logger) : IHandler<Guid, CheckNotificationResponse>
{
    public async Task<OneOf<CheckNotificationResponse, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking notification status for correspondence {CorrespondenceId}", correspondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, false, cancellationToken);
        var response = new CheckNotificationResponse
        {
            SendNotification = true
        };
        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found during notification check", correspondenceId);
            response.SendNotification = false;
            return response;
        }
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Read))
        {
            logger.LogInformation("Notification not needed for correspondence {CorrespondenceId} - already read", correspondenceId);
            response.SendNotification = false;
        }
        if (correspondence.StatusHasBeen(CorrespondenceStatus.PurgedByAltinn) || correspondence.StatusHasBeen(CorrespondenceStatus.PurgedByRecipient))
        {
            logger.LogInformation("Notification not needed for correspondence {CorrespondenceId} - has been purged", correspondenceId);
            response.SendNotification = false;
        }
        if (!correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            logger.LogInformation("Correspondence {CorrespondenceId} not yet published, scheduling notification check in 1 hour", correspondenceId);
            backgroundJobClient.Schedule<EnsureNotificationHandler>(handler => handler.Process(correspondenceId, null, CancellationToken.None), DateTimeOffset.Now.AddHours(1));
            response.SendNotification = false;
        }
        logger.LogInformation("Notification check completed for correspondence {CorrespondenceId} - SendNotification: {SendNotification}", correspondenceId, response.SendNotification);
        return response;
    }
}
