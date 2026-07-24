using Altinn.Correspondence.Application.CheckNotificationDelivery;
using Altinn.Correspondence.Application.SendSlackNotification;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CheckNotificationDeliveryHandlerTests
{
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
    private readonly Mock<ICorrespondenceNotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock;
    private readonly Mock<IAltinnNotificationService> _notificationServiceMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ILogger<CheckNotificationDeliveryHandler>> _loggerMock;
    private readonly CheckNotificationDeliveryHandler _handler;

    public CheckNotificationDeliveryHandlerTests()
    {
        _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
        _notificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
        _idempotencyKeyRepositoryMock = new Mock<IIdempotencyKeyRepository>();
        _notificationServiceMock = new Mock<IAltinnNotificationService>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _loggerMock = new Mock<ILogger<CheckNotificationDeliveryHandler>>();

        _handler = new CheckNotificationDeliveryHandler(
            _correspondenceRepositoryMock.Object,
            _notificationRepositoryMock.Object,
            _idempotencyKeyRepositoryMock.Object,
            _notificationServiceMock.Object,
            _backgroundJobClientMock.Object,
            _loggerMock.Object,
            TestDbContextFactory.Create());
    }

    [Fact]
    public async Task Process_WhenNotificationNotFound_ReturnsError()
    {
        var notificationId = Guid.NewGuid();
        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorrespondenceNotificationEntity?)null);

        var result = await _handler.Process(notificationId, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(3020, result.AsT1.ErrorCode);
    }

    [Fact]
    public async Task Process_WhenCorrespondenceNotFound_ReturnsError()
    {
        var (notification, _, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence: null, shipmentId);

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(1001, result.AsT1.ErrorCode);
    }

    [Fact]
    public async Task Process_WhenNotificationAlreadyMarkedAsSent_ReturnsTrue()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(notificationSent: DateTimeOffset.UtcNow.AddMinutes(-5));
        SetupMocks(notification, correspondence, shipmentId);

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0);
    }

    [Fact]
    public async Task Process_WhenV2NotificationDelivered_CreatesActivityAndMarksAsSent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0);
        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            notification.Id, It.IsAny<DateTimeOffset>(), "test@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IDialogportenService) && job.Method.Name == nameof(IDialogportenService.CreateInformationActivity)),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }

    [Fact]
    public async Task Process_WhenResolvedConcurrently_SkipsDuplicateSideEffects()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId));
        _idempotencyKeyRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(BuildUniqueViolation());

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0);
        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationDelivered, Times.Never());
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IDialogportenService) && job.Method.Name == nameof(IDialogportenService.CreateInformationActivity)),
            It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenNotificationNotDelivered_SchedulesNextCheckWithoutSideEffects()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_New, orderStatus: "Order_Processing"));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IDialogportenService) && job.Method.Name == nameof(IDialogportenService.CreateInformationActivity)),
            It.IsAny<IState>()), Times.Never);
        VerifyDeliveryCheckRescheduled(Times.Once());
    }

    [Fact]
    public async Task Process_WhenNotDeliveredOnFinalAttempt_GivesUpAndAlertsSlack()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_New, orderStatus: "Order_Processing"));

        var result = await _handler.Process(notification.Id, CancellationToken.None, publishFailedEvent: true, attempt: CheckNotificationDeliveryHandler.MaxAttempts);

        Assert.True(result.IsT0);
        VerifyDeliveryCheckRescheduled(Times.Never());
        VerifySlackNotificationEnqueued(Times.Once());
    }

    [Fact]
    public async Task Process_WhenNotDeliveredOnAttemptBeforeFinal_ReschedulesWithoutAlertingSlack()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_New, orderStatus: "Order_Processing"));

        var result = await _handler.Process(notification.Id, CancellationToken.None, publishFailedEvent: true, attempt: CheckNotificationDeliveryHandler.MaxAttempts - 1);

        Assert.True(result.IsT0);
        VerifyDeliveryCheckRescheduled(Times.Once());
        VerifySlackNotificationEnqueued(Times.Never());
    }

    [Theory]
    [InlineData(NotificationStatusV2.Email_Failed)]
    [InlineData(NotificationStatusV2.Email_Failed_Bounced)]
    [InlineData(NotificationStatusV2.Email_Failed_Quarantined)]
    [InlineData(NotificationStatusV2.SMS_Failed)]
    [InlineData(NotificationStatusV2.SMS_Failed_TTL)]
    [InlineData(NotificationStatusV2.SMS_Failed_RecipientNotIdentified)]
    public async Task Process_WhenAllNotificationsFail_CreatesAllFailedEvent(NotificationStatusV2 recipientStatus)
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, recipientStatus));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenSomeRecipientsFail_CreatesPartialFailedEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, partialFail: true));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenNotificationAlreadyHasTerminalOrderStatus_SkipsReprocessing()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        notification.NotificationOrderStatus = "Order_Completed";
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0);
        // Short-circuits before querying delivery status or producing any side effects
        _notificationServiceMock.Verify(x => x.GetNotificationDetailsV2(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyActivityCreated(DialogportenTextType.NotificationFailed, Times.Never());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Never());
    }

    [Theory]
    [InlineData(NotificationStatusV2.Email_Failed)]
    [InlineData(NotificationStatusV2.Email_Failed_Bounced)]
    [InlineData(NotificationStatusV2.Email_Failed_RecipientReserved)]
    [InlineData(NotificationStatusV2.SMS_Failed)]
    [InlineData(NotificationStatusV2.SMS_Failed_RecipientNotIdentified)]
    public async Task Process_WhenNotificationFailsWithHardFailure_CreatesNotificationFailedActivity(NotificationStatusV2 recipientStatus)
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, recipientStatus));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyActivityCreated(DialogportenTextType.NotificationFailed, Times.Once());
        VerifyActivityCreated(DialogportenTextType.NotificationDeliveryUnconfirmed, Times.Never());
    }

    [Theory]
    [InlineData(NotificationStatusV2.Email_Failed_TTL)]
    [InlineData(NotificationStatusV2.SMS_Failed_TTL)]
    public async Task Process_WhenNotificationFailsWithTtl_CreatesDeliveryUnconfirmedActivity(NotificationStatusV2 recipientStatus)
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, recipientStatus));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyActivityCreated(DialogportenTextType.NotificationDeliveryUnconfirmed, Times.Once());
        VerifyActivityCreated(DialogportenTextType.NotificationFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenReminderFailsWithHardFailure_CreatesReminderFailedActivity()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(isReminder: true);
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyActivityCreated(DialogportenTextType.NotificationReminderFailed, Times.Once());
        VerifyActivityCreated(DialogportenTextType.NotificationFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenReminderFailsWithTtl_CreatesReminderDeliveryUnconfirmedActivity()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(isReminder: true);
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed_TTL));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyActivityCreated(DialogportenTextType.NotificationReminderDeliveryUnconfirmed, Times.Once());
        VerifyActivityCreated(DialogportenTextType.NotificationDeliveryUnconfirmed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenSomeRecipientsFail_CreatesActivityForBothSentAndFailedRecipients()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, partialFail: true));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyActivityCreated(DialogportenTextType.NotificationSent, Times.Once());
        VerifyActivityCreated(DialogportenTextType.NotificationFailed, Times.Once());
    }

    [Fact]
    public async Task Process_WhenMainNotificationSent_CreatesNotificationSentEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario();
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationDelivered, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationReminderDelivered, Times.Never());
    }

    [Fact]
    public async Task Process_WhenReminderNotificationSent_CreatesReminderSentEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(isReminder: true);
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId));

        var result = await _handler.Process(notification.Id, CancellationToken.None);

        Assert.True(result.IsT0);
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationReminderDelivered, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationDelivered, Times.Never());
    }

    [Fact]
    public async Task Process_WhenMainOrgOrderAllFail_CreatesAllFailedEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(
            correspondenceRecipient: "urn:altinn:organization:identifier-no:123456789",
            orderRecipient: new RecipientV2
            {
                RecipientOrganization = new RecipientOrganization { OrgNumber = "123456789", ResourceId = "r", ChannelSchema = NotificationChannel.Email }
            });
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenMainPersonOrderAllFail_CreatesAllFailedEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(
            correspondenceRecipient: "urn:altinn:person:identifier-no:12345678901",
            orderRecipient: new RecipientV2
            {
                RecipientPerson = new RecipientPerson { NationalIdentityNumber = "12345678901", ResourceId = "r", ChannelSchema = NotificationChannel.Email }
            });
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Never());
    }

    [Theory]
    [InlineData("urn:altinn:person:idporten-email:test@example.com")]
    [InlineData("urn:altinn:person:legacy-selfidentified:testuser")]
    public async Task Process_WhenMainExternalIdentityOrderAllFail_CreatesAllFailedEvent(string correspondenceRecipient)
    {
        var (notification, correspondence, shipmentId) = BuildScenario(
            correspondenceRecipient: correspondenceRecipient,
            orderRecipient: new RecipientV2
            {
                RecipientExternalIdentity = new RecipientExternalIdentity { ExternalIdentity = correspondenceRecipient, ResourceId = "r", ChannelSchema = NotificationChannel.Email }
            });
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenCustomOrgRecipientOrderAllFail_CreatesFailedNotAllFailedEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(
            correspondenceRecipient: "urn:altinn:organization:identifier-no:123456789",
            orderRecipient: new RecipientV2
            {
                RecipientOrganization = new RecipientOrganization { OrgNumber = "987654321", ResourceId = "r", ChannelSchema = NotificationChannel.Email }
            });
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenCustomEmailRecipientOrderAllFail_CreatesFailedNotAllFailedEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(
            correspondenceRecipient: "urn:altinn:organization:identifier-no:123456789",
            orderRecipient: new RecipientV2
            {
                RecipientEmail = new RecipientEmail { EmailAddress = "custom@example.com" }
            });
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Never());
    }

    [Fact]
    public async Task Process_WhenNullOrderRequestAllFail_CreatesAllFailedEvent()
    {
        var (notification, correspondence, shipmentId) = BuildScenario(
            correspondenceRecipient: "urn:altinn:organization:identifier-no:123456789",
            orderRecipient: null);
        SetupMocks(notification, correspondence, shipmentId, BuildStatus(shipmentId, NotificationStatusV2.Email_Failed));

        await _handler.Process(notification.Id, CancellationToken.None);

        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationAllFailed, Times.Once());
        VerifyEventPublished(AltinnEventType.CorrespondenceNotificationFailed, Times.Never());
    }

    private static (CorrespondenceNotificationEntity notification, CorrespondenceEntity correspondence, Guid shipmentId) BuildScenario(
        string correspondenceRecipient = "urn:altinn:person:identifier-no:12345678901",
        RecipientV2? orderRecipient = null,
        bool isReminder = false,
        DateTimeOffset? notificationSent = null)
    {
        var correspondenceId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var notification = new CorrespondenceNotificationEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondenceId,
            ShipmentId = shipmentId,
            IsReminder = isReminder,
            NotificationChannel = NotificationChannel.Email,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow,
            NotificationSent = notificationSent,
            OrderRequest = orderRecipient == null ? null : JsonSerializer.Serialize(new NotificationOrderRequestV2
            {
                IdempotencyId = Guid.NewGuid(),
                SendersReference = "test",
                RequestedSendTime = DateTime.UtcNow,
                Recipient = orderRecipient
            })
        };
        var correspondence = new CorrespondenceEntity
        {
            Id = correspondenceId,
            Recipient = correspondenceRecipient,
            Sender = "test_sender",
            ResourceId = "test_resource",
            SendersReference = "test_reference",
            RequestedPublishTime = DateTimeOffset.UtcNow,
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow
        };
        return (notification, correspondence, shipmentId);
    }

    private void SetupMocks(
        CorrespondenceNotificationEntity notification,
        CorrespondenceEntity? correspondence,
        Guid shipmentId,
        NotificationStatusResponseV2? notificationStatus = null)
    {
        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(notification.CorrespondenceId, true, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondence);
        if (notificationStatus != null)
        {
            _notificationServiceMock.Setup(x => x.GetNotificationDetailsV2(shipmentId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(notificationStatus);
        }
    }

    private static NotificationStatusResponseV2 BuildStatus(
        Guid shipmentId,
        NotificationStatusV2 recipientStatus = NotificationStatusV2.Email_Delivered,
        string orderStatus = "Order_Completed",
        bool partialFail = false)
    {
        var type = recipientStatus >= NotificationStatusV2.SMS_New && recipientStatus < NotificationStatusV2.Unknown
            ? NotificationType.SMS
            : NotificationType.Email;
        var recipients = partialFail
            ? (List<RecipientStatus>)
            [
                new() { Type = NotificationType.Email, Destination = "delivered@example.com", Status = NotificationStatusV2.Email_Delivered, LastUpdate = DateTimeOffset.UtcNow },
                new() { Type = NotificationType.Email, Destination = "failed@example.com", Status = NotificationStatusV2.Email_Failed, LastUpdate = DateTimeOffset.UtcNow }
            ]
            :
            [
                new() { Type = type, Destination = "test@example.com", Status = recipientStatus, LastUpdate = DateTimeOffset.UtcNow }
            ];
        return new NotificationStatusResponseV2 { Status = orderStatus, ShipmentId = shipmentId, Recipients = recipients };
    }

    private void VerifyActivityCreated(DialogportenTextType textType, Times times)
    {
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(IDialogportenService) &&
                job.Method.Name == nameof(IDialogportenService.CreateInformationActivity) &&
                job.Args.Count > 2 &&
                job.Args[2] is DialogportenTextType &&
                (DialogportenTextType)job.Args[2] == textType),
            It.Is<IState>(state => state is EnqueuedState)), times);
    }

    private void VerifyDeliveryCheckRescheduled(Times times)
    {
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(CheckNotificationDeliveryHandler) &&
                job.Method.Name == nameof(CheckNotificationDeliveryHandler.Process)),
            It.Is<IState>(state => state is ScheduledState)), times);
    }

    private static DbUpdateException BuildUniqueViolation()
    {
        var inner = new Exception("duplicate key value violates unique constraint");
        inner.Data["SqlState"] = "23505";
        return new DbUpdateException("unique violation", inner);
    }

    private void VerifySlackNotificationEnqueued(Times times)
    {
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(SendSlackNotificationHandler) &&
                job.Method.Name == nameof(SendSlackNotificationHandler.Process)),
            It.Is<IState>(state => state is EnqueuedState)), times);
    }

    private void VerifyEventPublished(AltinnEventType eventType, Times times)
    {
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(IEventBus) &&
                job.Method.Name == nameof(IEventBus.Publish) &&
                (AltinnEventType)job.Args[0] == eventType),
            It.Is<IState>(state => state is EnqueuedState)), times);
    }
}
