using Altinn.Correspondence.Application.CheckForwardedCorrespondenceDelivery;
using Altinn.Correspondence.Application.SendSlackNotification;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CheckForwardedCorrespondenceDeliveryHandlerTests
{
    private readonly Mock<IAltinnNotificationService> _notificationServiceMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock = new();
    private readonly Mock<ICorrespondenceForwardingEventRepository> _forwardingEventRepositoryMock = new();
    private readonly Mock<ILogger<CheckForwardedCorrespondenceDeliveryHandler>> _loggerMock = new();

    private readonly CheckForwardedCorrespondenceDeliveryHandler _handler;

    public CheckForwardedCorrespondenceDeliveryHandlerTests()
    {
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        _handler = new CheckForwardedCorrespondenceDeliveryHandler(
            _notificationServiceMock.Object,
            _backgroundJobClientMock.Object,
            _correspondenceStatusRepositoryMock.Object,
            _forwardingEventRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Process_NotificationDetailsNotYetAvailable_SchedulesNextPoll()
    {
        var shipmentId = Guid.NewGuid();
        var forwardingEventId = Guid.NewGuid();
        _notificationServiceMock
            .Setup(x => x.GetNotificationDetailsV2(shipmentId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationStatusResponseV2?)null);

        await _handler.Process(shipmentId, forwardingEventId, CancellationToken.None);

        VerifyDeliveryCheckRescheduled(Times.Once());
        _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_EmailDelivered_AddsForwardedStatusAndQueuesDialogportenActivity()
    {
        var forwardingEvent = BuildForwardingEvent();
        var shipmentId = Guid.NewGuid();
        SetupNotification(shipmentId, forwardingEvent, BuildStatus(shipmentId, NotificationStatusV2.Email_Delivered));

        await _handler.Process(shipmentId, forwardingEvent.Id, CancellationToken.None);

        _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(
            It.Is<CorrespondenceStatusEntity>(s =>
                s.CorrespondenceId == forwardingEvent.CorrespondenceId &&
                s.Status == CorrespondenceStatus.Forwarded &&
                s.PartyUuid == forwardingEvent.ForwardedByPartyUuid),
            It.IsAny<CancellationToken>()), Times.Once);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(IDialogportenService) &&
                job.Method.Name == nameof(IDialogportenService.AddForwardingEvent) &&
                (Guid)job.Args[0] == forwardingEvent.Id),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
        VerifyDeliveryCheckRescheduled(Times.Never());
    }

    [Fact]
    public async Task Process_AllRecipientsFailed_LogsAndDoesNotReschedule()
    {
        var forwardingEvent = BuildForwardingEvent();
        var shipmentId = Guid.NewGuid();
        SetupNotification(shipmentId, forwardingEvent, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(shipmentId, forwardingEvent.Id, CancellationToken.None);

        _correspondenceStatusRepositoryMock.Verify(x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IDialogportenService)),
            It.IsAny<IState>()), Times.Never);
        VerifyDeliveryCheckRescheduled(Times.Never());
        VerifySlackNotificationEnqueued(Times.Never());
    }

    [Fact]
    public async Task Process_NotYetDelivered_SchedulesNextPollAttempt()
    {
        var forwardingEvent = BuildForwardingEvent();
        var shipmentId = Guid.NewGuid();
        SetupNotification(shipmentId, forwardingEvent, BuildStatus(shipmentId, NotificationStatusV2.Email_New));

        await _handler.Process(shipmentId, forwardingEvent.Id, CancellationToken.None, attempt: 1);

        VerifyDeliveryCheckRescheduled(Times.Once());
        VerifySlackNotificationEnqueued(Times.Never());
    }

    [Fact]
    public async Task Process_NotDeliveredOnFinalAttempt_GivesUpAndAlertsSlack()
    {
        var forwardingEvent = BuildForwardingEvent();
        var shipmentId = Guid.NewGuid();
        SetupNotification(shipmentId, forwardingEvent, BuildStatus(shipmentId, NotificationStatusV2.Email_New));

        await _handler.Process(shipmentId, forwardingEvent.Id, CancellationToken.None, attempt: CheckForwardedCorrespondenceDeliveryHandler.MaxAttempts);

        VerifyDeliveryCheckRescheduled(Times.Never());
        VerifySlackNotificationEnqueued(Times.Once());
    }

    [Fact]
    public async Task Process_NotDeliveredOnAttemptBeforeFinal_ReschedulesWithoutAlertingSlack()
    {
        var forwardingEvent = BuildForwardingEvent();
        var shipmentId = Guid.NewGuid();
        SetupNotification(shipmentId, forwardingEvent, BuildStatus(shipmentId, NotificationStatusV2.Email_New));

        await _handler.Process(shipmentId, forwardingEvent.Id, CancellationToken.None, attempt: CheckForwardedCorrespondenceDeliveryHandler.MaxAttempts - 1);

        VerifyDeliveryCheckRescheduled(Times.Once());
        VerifySlackNotificationEnqueued(Times.Never());
    }

    private static CorrespondenceForwardingEventEntity BuildForwardingEvent()
    {
        var correspondence = new CorrespondenceEntityBuilder().Build();
        return new CorrespondenceForwardingEventEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondence.Id,
            Correspondence = correspondence,
            ForwardedOnDate = DateTimeOffset.UtcNow,
            ForwardedByPartyUuid = Guid.NewGuid(),
            ForwardedByUserUuid = Guid.NewGuid(),
            ForwardedToEmailAddress = "recipient@example.com"
        };
    }

    private void SetupNotification(Guid shipmentId, CorrespondenceForwardingEventEntity forwardingEvent, NotificationStatusResponseV2? status)
    {
        if (status != null)
        {
            _notificationServiceMock
                .Setup(x => x.GetNotificationDetailsV2(shipmentId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(status);
        }
        _forwardingEventRepositoryMock
            .Setup(x => x.GetForwardingEvent(forwardingEvent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(forwardingEvent);
    }

    private static NotificationStatusResponseV2 BuildStatus(Guid shipmentId, NotificationStatusV2 recipientStatus)
    {
        return new NotificationStatusResponseV2
        {
            Status = "Order_Processing",
            ShipmentId = shipmentId,
            Recipients =
            [
                new RecipientStatus { Type = NotificationType.Email, Destination = "recipient@example.com", Status = recipientStatus, LastUpdate = DateTimeOffset.UtcNow }
            ]
        };
    }

    private void VerifyDeliveryCheckRescheduled(Times times)
    {
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(CheckForwardedCorrespondenceDeliveryHandler) &&
                job.Method.Name == nameof(CheckForwardedCorrespondenceDeliveryHandler.Process)),
            It.Is<IState>(state => state is ScheduledState)), times);
    }

    private void VerifySlackNotificationEnqueued(Times times)
    {
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(SendSlackNotificationHandler) &&
                job.Method.Name == nameof(SendSlackNotificationHandler.Process)),
            It.Is<IState>(state => state is EnqueuedState)), times);
    }
}
