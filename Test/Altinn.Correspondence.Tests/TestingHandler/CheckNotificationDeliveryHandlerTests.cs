using Altinn.Correspondence.Application.CheckNotificationDelivery;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CheckNotificationDeliveryHandlerTests
{
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
    private readonly Mock<ICorrespondenceNotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IAltinnNotificationService> _notificationServiceMock;
    private readonly Mock<IDialogportenService> _dialogportenServiceMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ILogger<CheckNotificationDeliveryHandler>> _loggerMock;
    private readonly CheckNotificationDeliveryHandler _handler;

    public CheckNotificationDeliveryHandlerTests()
    {
        _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
        _notificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
        _notificationServiceMock = new Mock<IAltinnNotificationService>();
        _dialogportenServiceMock = new Mock<IDialogportenService>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _loggerMock = new Mock<ILogger<CheckNotificationDeliveryHandler>>();

        _handler = new CheckNotificationDeliveryHandler(
            _correspondenceRepositoryMock.Object,
            _notificationRepositoryMock.Object,
            _notificationServiceMock.Object,
            _dialogportenServiceMock.Object,
            _backgroundJobClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Process_WhenNotificationNotFound_ReturnsError()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorrespondenceNotificationEntity?)null);

        // Act
        var result = await _handler.Process(notificationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsT1);
        Assert.Equal(3020, result.AsT1.ErrorCode);
    }

    [Fact]
    public async Task Process_WhenCorrespondenceNotFound_ReturnsError()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var correspondenceId = Guid.NewGuid();
        var notification = new CorrespondenceNotificationEntity
        {
            Id = notificationId,
            CorrespondenceId = correspondenceId,
            NotificationOrderId = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow
        };

        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync((CorrespondenceEntity?)null);

        // Act
        var result = await _handler.Process(notificationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsT1);
        Assert.Equal(1001, result.AsT1.ErrorCode);
    }

    [Fact]
    public async Task Process_WhenNotificationAlreadyMarkedAsSent_ReturnsTrue()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var correspondenceId = Guid.NewGuid();
        var notification = new CorrespondenceNotificationEntity
        {
            Id = notificationId,
            CorrespondenceId = correspondenceId,
            NotificationOrderId = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow,
            NotificationSent = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var correspondence = new CorrespondenceEntity
        {
            Id = correspondenceId,
            Recipient = "12345678901",
            Sender = "test_sender",
            ResourceId = "test_resource",
            SendersReference = "test_reference",
            RequestedPublishTime = DateTimeOffset.UtcNow,
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow
        };

        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);

        // Act
        var result = await _handler.Process(notificationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(result.AsT0);
    }

    [Fact]
    public async Task Process_WhenV2NotificationDelivered_CreatesActivityAndMarksAsSent()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var correspondenceId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var notification = new CorrespondenceNotificationEntity
        {
            Id = notificationId,
            CorrespondenceId = correspondenceId,
            ShipmentId = shipmentId,
            NotificationChannel = NotificationChannel.Email,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow
        };

        var correspondence = new CorrespondenceEntity
        {
            Id = correspondenceId,
            Recipient = "12345678901",
            Sender = "test_sender",
            ResourceId = "test_resource",
            SendersReference = "test_reference",
            RequestedPublishTime = DateTimeOffset.UtcNow,
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow
        };

        var notificationDetailsV2 = new NotificationStatusResponseV2
        {
            Status = "Order_Completed",
            ShipmentId = shipmentId,
            Recipients = new List<RecipientStatus>
            {
                new()
                {
                    Type = NotificationType.Email,
                    Destination = "test@example.com",
                    Status = NotificationStatusV2.Email_Delivered,
                    LastUpdate = DateTimeOffset.UtcNow
                }
            }
        };

        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _notificationServiceMock.Setup(x => x.GetNotificationDetailsV2(shipmentId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificationDetailsV2);

        // Act
        var result = await _handler.Process(notificationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);
        Assert.True(result.AsT0);

        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            notificationId,
            It.IsAny<DateTimeOffset>(),
            "test@example.com",
            It.IsAny<CancellationToken>()), Times.Once);

        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(IDialogportenService) &&
                job.Method.Name == nameof(IDialogportenService.CreateInformationActivity)),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }

    [Fact]
    public async Task Process_WhenNotificationNotDelivered_ThrowsException()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var correspondenceId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var notification = new CorrespondenceNotificationEntity
        {
            Id = notificationId,
            CorrespondenceId = correspondenceId,
            ShipmentId = shipmentId,
            NotificationChannel = NotificationChannel.Email,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow
        };
        var correspondence = new CorrespondenceEntity
        {
            Id = correspondenceId,
            Recipient = "12345678901",
            Sender = "test_sender",
            ResourceId = "test_resource",
            SendersReference = "test_reference",
            RequestedPublishTime = DateTimeOffset.UtcNow,
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow
        };
        var notificationDetailsV2 = new NotificationStatusResponseV2
        {
            Status = "Order_Processing",
            ShipmentId = shipmentId,
            Recipients = new List<RecipientStatus>
        {
            new()
            {
                Type = NotificationType.Email,
                Destination = "test@example.com",
                Status = NotificationStatusV2.Email_New,
                LastUpdate = DateTimeOffset.UtcNow
            }
        }
        };
        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _notificationServiceMock.Setup(x => x.GetNotificationDetailsV2(shipmentId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificationDetailsV2);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Process(notificationId, CancellationToken.None)
        );

        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            It.IsAny<Guid>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _dialogportenServiceMock.Verify(x => x.CreateInformationActivity(
            It.IsAny<Guid>(),
            It.IsAny<DialogportenActorType>(),
            It.IsAny<DialogportenTextType>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationStatusV2.Email_Failed)]
    [InlineData(NotificationStatusV2.Email_Failed_Bounced)]
    [InlineData(NotificationStatusV2.Email_Failed_Quarantined)]
    [InlineData(NotificationStatusV2.SMS_Failed)]
    [InlineData(NotificationStatusV2.SMS_Failed_TTL)]
    [InlineData(NotificationStatusV2.SMS_Failed_RecipientNotIdentified)]
    public async Task Process_WhenNotificationFails_CreateFailedEvent(NotificationStatusV2 recipientStatus)
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var correspondenceId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var notification = new CorrespondenceNotificationEntity
        {
            Id = notificationId,
            CorrespondenceId = correspondenceId,
            ShipmentId = shipmentId,
            NotificationChannel = NotificationChannel.Email,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            Created = DateTimeOffset.UtcNow,
            RequestedSendTime = DateTimeOffset.UtcNow
        };
        var correspondence = new CorrespondenceEntity
        {
            Id = correspondenceId,
            Recipient = "12345678901",
            Sender = "test_sender",
            ResourceId = "test_resource",
            SendersReference = "test_reference",
            RequestedPublishTime = DateTimeOffset.UtcNow,
            Statuses = new List<CorrespondenceStatusEntity>(),
            Created = DateTimeOffset.UtcNow
        };
        var notificationDetailsV2 = new NotificationStatusResponseV2
        {
            Status = "Order_Completed",
            ShipmentId = shipmentId,
            Recipients = new List<RecipientStatus>
        {
            new()
            {
                Type = recipientStatus >= NotificationStatusV2.SMS_New && recipientStatus < NotificationStatusV2.Unknown
                    ? NotificationType.SMS
                    : NotificationType.Email,
                Destination = "test@example.com",
                Status = recipientStatus,
                LastUpdate = DateTimeOffset.UtcNow
            }
        }
        };
        _notificationRepositoryMock.Setup(x => x.GetNotificationById(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _correspondenceRepositoryMock.Setup(x => x.GetCorrespondenceById(correspondenceId, true, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        _notificationServiceMock.Setup(x => x.GetNotificationDetailsV2(shipmentId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificationDetailsV2);

        // Act
        var result = await _handler.Process(notificationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);

        _notificationRepositoryMock.Verify(x => x.UpdateNotificationSent(
            It.IsAny<Guid>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _dialogportenServiceMock.Verify(x => x.CreateInformationActivity(
            It.IsAny<Guid>(),
            It.IsAny<DialogportenActorType>(),
            It.IsAny<DialogportenTextType>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(IEventBus) &&
                job.Method.Name == nameof(IEventBus.Publish) &&
                (AltinnEventType)job.Args[0] == AltinnEventType.CorrespondenceNotificationFailed &&
                (string)job.Args[1] == correspondence.ResourceId &&
                (string)job.Args[2] == correspondence.Id.ToString() &&
                (string)job.Args[3] == "correspondence" &&
                (string)job.Args[4] == correspondence.Sender),
            It.Is<IState>(state => state is EnqueuedState)), Times.Once);
    }
}
