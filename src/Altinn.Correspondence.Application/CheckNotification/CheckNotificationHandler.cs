using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using OneOf;
using System.Security.Claims;
using Slack.Webhooks;

namespace Altinn.Correspondence.Application.CheckNotification;

public class CheckNotificationHandler(
    ICorrespondenceRepository correspondenceRepository,
    ILogger<CheckNotificationHandler> logger,
    IHostEnvironment hostEnvironment,
    ISlackClient slackClient,
    SlackSettings slackSettings) : IHandler<Guid, CheckNotificationResponse>
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
        if (correspondence.StatusHasBeen(CorrespondenceStatus.Failed))
        {
            logger.LogError("Notification not needed for correspondence {CorrespondenceId} - correspondence has failed", correspondenceId);
            var errorMessage = $"Notification should not be sendt for correspondence {correspondenceId} - correspondence has failed";
            var slackSent = await SlackHelper.SendSlackNotificationWithMessage(
                "A notification order was sendt for a correspondence that has failed",
                errorMessage,
                slackClient,
                slackSettings.NotificationChannel,
                hostEnvironment.EnvironmentName);
            if (!slackSent)
            {
                logger.LogError("Failed to send Slack notification for correspondence {CorrespondenceId}", correspondenceId);
            }
            response.SendNotification = false;
        }
        else if (!correspondence.StatusHasBeen(CorrespondenceStatus.Published))
        {
            logger.LogError("Notification not needed for correspondence {CorrespondenceId} - correspondence has not been published", correspondenceId);
            var errorMessage = $"Notification should not be sendt for correspondence {correspondenceId} - correspondence has not been published";
            var slackSent = await SlackHelper.SendSlackNotificationWithMessage(
                "A notification order was sendt for a correspondence that has not been published",
                errorMessage,
                slackClient,
                slackSettings.NotificationChannel,
                hostEnvironment.EnvironmentName);
            if (!slackSent)
            {
                logger.LogError("Failed to send Slack notification for correspondence {CorrespondenceId}", correspondenceId);
            }
            response.SendNotification = false;
        }
        logger.LogInformation("Notification check completed for correspondence {CorrespondenceId} - SendNotification: {SendNotification}", correspondenceId, response.SendNotification);
        return response;
    }
}
