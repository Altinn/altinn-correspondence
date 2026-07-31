using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.SendSlackNotification;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CheckForwardedCorrespondenceDelivery;

public class CheckForwardedCorrespondenceDeliveryHandler(
    IAltinnNotificationService altinnNotificationService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CheckForwardedCorrespondenceDeliveryHandler> logger)
{
    private static readonly int[] PollBackoffSeconds =
    [
        60,
        15 * 60,
        4 * 60 * 60,
        8 * 60 * 60,
        12 * 60 * 60,
        16 * 60 * 60,
        36 * 60 * 60,
        48 * 60 * 60,
        72 * 60 * 60
    ];

    public static readonly int MaxAttempts = PollBackoffSeconds.Length + 1;

    public async Task Process(Guid shipmentId, Guid forwardingEventId, CancellationToken cancellationToken)
    {
        await Process(shipmentId, forwardingEventId, cancellationToken, attempt: 1);
    }

    public async Task Process(Guid shipmentId, Guid forwardingEventId, CancellationToken cancellationToken, int attempt)
    {
        logger.LogInformation("Checking delivery status for forwarded correspondence shipment {ShipmentId}", shipmentId);

        var notification = await altinnNotificationService.GetNotificationDetailsV2(shipmentId.ToString(), cancellationToken);
        if (notification == null)
        {
            ScheduleNextPollOrGiveUp(shipmentId, forwardingEventId, attempt, "notification details not yet available");
            return;
        }

        if (notification.Recipients.Any(r => r.Status == NotificationStatusV2.Email_Delivered))
        {
            logger.LogInformation(
                "Forwarded correspondence shipment {ShipmentId} has been delivered. Queuing Dialogporten activity update for forwarding event {ForwardingEventId}.",
                shipmentId, forwardingEventId);
            backgroundJobClient.Enqueue<IDialogportenService>(x => x.AddForwardingEvent(forwardingEventId, CancellationToken.None));
            return;
        }

        if (notification.Recipients.Count != 0 && notification.Recipients.All(r => r.Status.IsFailed()))
        {
            logger.LogError(
                "Forwarded correspondence shipment {ShipmentId} failed to be delivered for forwarding event {ForwardingEventId}.",
                shipmentId, forwardingEventId);
            return;
        }

        ScheduleNextPollOrGiveUp(shipmentId, forwardingEventId, attempt, "not yet delivered");
    }

    private void ScheduleNextPollOrGiveUp(Guid shipmentId, Guid forwardingEventId, int attempt, string reason)
    {
        if (attempt >= MaxAttempts)
        {
            logger.LogError(
                "Delivery for forwarded correspondence shipment {ShipmentId} (forwarding event {ForwardingEventId}) was not confirmed after {MaxAttempts} attempts ({Reason}). Giving up polling.",
                shipmentId, forwardingEventId, MaxAttempts, reason);
            var slackMessage = $"Forwarding shipment {shipmentId} for forwarding event {forwardingEventId} did not reach a delivered status after {MaxAttempts} delivery checks ({reason}). Delivery polling has been stopped.";
            backgroundJobClient.Enqueue<SendSlackNotificationHandler>(handler =>
                handler.Process("Forwarded correspondence delivery not confirmed", slackMessage, ":warning:"));
            return;
        }

        var nextAttempt = attempt + 1;
        var nextPollTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(PollBackoffSeconds[attempt - 1]);
        backgroundJobClient.Schedule<CheckForwardedCorrespondenceDeliveryHandler>(HangfireQueues.Default,
            handler => handler.Process(shipmentId, forwardingEventId, CancellationToken.None, nextAttempt),
            nextPollTime);
        logger.LogInformation(
            "Forwarded correspondence shipment {ShipmentId} delivery not yet confirmed on attempt {Attempt} of {MaxAttempts} ({Reason}). Scheduled attempt {NextAttempt} at {NextPollTime}.",
            shipmentId, attempt, MaxAttempts, reason, nextAttempt, nextPollTime);
    }
}
