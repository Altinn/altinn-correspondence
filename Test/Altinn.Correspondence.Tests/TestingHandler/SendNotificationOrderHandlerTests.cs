using Moq;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Application.SendNotificationOrder;
using Microsoft.Extensions.Logging;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Altinn.Correspondence.Application.CheckNotificationDelivery;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SendNotificationOrderHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
        private readonly Mock<ICorrespondenceNotificationRepository> _mockCorrespondenceNotificationRepository;
        private readonly Mock<IAltinnNotificationService> _mockAltinnNotificationService;
        private readonly Mock<IIdempotencyKeyRepository> _mockIdempotencyKeyRepository;
        private readonly Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private readonly Mock<ILogger<SendNotificationOrderHandler>> _mockLogger;
        private readonly SendNotificationOrderHandler _handler;

        public SendNotificationOrderHandlerTests()
        {
            _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
            _mockCorrespondenceNotificationRepository = new Mock<ICorrespondenceNotificationRepository>();
            _mockAltinnNotificationService = new Mock<IAltinnNotificationService>();
            _mockIdempotencyKeyRepository = new Mock<IIdempotencyKeyRepository>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
            _mockLogger = new Mock<ILogger<SendNotificationOrderHandler>>();

            _handler = new SendNotificationOrderHandler(
                _mockCorrespondenceNotificationRepository.Object,
                _mockCorrespondenceRepository.Object,
                _mockAltinnNotificationService.Object,
                _mockIdempotencyKeyRepository.Object,
                _mockBackgroundJobClient.Object,
                _mockLogger.Object);
        }

        private (Guid correspondenceId, CorrespondenceEntity correspondence, CorrespondenceNotificationEntity notification, NotificationOrderRequestV2 orderRequest, NotificationOrderRequestResponseV2 response) SetupData()
        {
            var correspondenceId = Guid.NewGuid();
            var notificationId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();
            var reminderShipmentId = Guid.NewGuid();

            var correspondence = new CorrespondenceEntity
            {
                Id = correspondenceId,
                ResourceId = "resource1",
                SendersReference = "ref1",
                Recipient = "urn:altinn:person:identifier-no:08900499559",
                RequestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10),
                Sender = "sender",
                Statuses = new List<CorrespondenceStatusEntity>(),
                Created = DateTimeOffset.UtcNow
            };

            var orderRequest = new NotificationOrderRequestV2
            {
                SendersReference = correspondence.SendersReference,
                RequestedSendTime = DateTime.UtcNow.AddMinutes(5),
                IdempotencyId = Guid.NewGuid(),
                Recipient = new RecipientV2 { RecipientEmail = new RecipientEmail { EmailAddress = "a@b.no" } },
                Reminders = new List<ReminderV2> { new ReminderV2 { DelayDays = 1, Recipient = new RecipientV2 { RecipientSms = new RecipientSms { PhoneNumber = "+47 12345678" } } } }
            };

            var notification = new CorrespondenceNotificationEntity
            {
                Created = DateTimeOffset.UtcNow,
                Id = notificationId,
                NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                NotificationChannel = NotificationChannel.EmailPreferred,
                CorrespondenceId = correspondenceId,
                RequestedSendTime = DateTime.UtcNow.AddMinutes(5),
                IsReminder = false,
                OrderRequest = JsonSerializer.Serialize(orderRequest)
            };

            var response = new NotificationOrderRequestResponseV2
            {
                NotificationOrderId = orderId,
                Notification = new NotificationResponseV2
                {
                    ShipmentId = shipmentId,
                    SendersReference = correspondence.SendersReference,
                    Reminders = new List<ReminderResponse> { new ReminderResponse { ShipmentId = reminderShipmentId, SendersReference = correspondence.SendersReference } }
                }
            };

            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(correspondenceId, false, false, false, CancellationToken.None, false))
                .ReturnsAsync(correspondence);
            _mockCorrespondenceNotificationRepository.Setup(x => x.GetPrimaryNotificationsByCorrespondenceId(correspondenceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CorrespondenceNotificationEntity> { notification });
            _mockAltinnNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
            _mockCorrespondenceNotificationRepository.Setup(x => x.UpdateOrderResponseData(notificationId, orderId, shipmentId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockCorrespondenceNotificationRepository.Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());

            return (correspondenceId, correspondence, notification, orderRequest, response);
        }

        [Fact]
        public async Task Process_ShouldUpdateOrderData_AndScheduleDelivery_ForMainAndReminder()
        {
            var (correspondenceId, _, _, _, response) = SetupData();

            await _handler.Process(correspondenceId, CancellationToken.None);

            _mockCorrespondenceNotificationRepository.Verify(x => x.UpdateOrderResponseData(It.IsAny<Guid>(), response.NotificationOrderId, response.Notification.ShipmentId, It.IsAny<CancellationToken>()), Times.Once);
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job =>
                    job.Type == typeof(Application.CheckNotificationDelivery.CheckNotificationDeliveryHandler) &&
                    job.Method.Name == nameof(Application.CheckNotificationDelivery.CheckNotificationDeliveryHandler.Process)),
                It.Is<IState>(state => state is ScheduledState)),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Process_ShouldPublishFailedEvent_WhenAnySendFails()
        {
            var (correspondenceId, _, _, _, _) = SetupData();
            _mockAltinnNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NotificationOrderRequestResponseV2?)null);

            await _handler.Process(correspondenceId, CancellationToken.None);

            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job =>
                    job.Type == typeof(IEventBus) &&
                    job.Method.Name == "Publish"),
                It.Is<IState>(state => state is EnqueuedState)),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Process_ShouldSkipReminderPersist_WhenIdempotencyKeyExists()
        {
            // Arrange
            var (correspondenceId, _, _, _, response) = SetupData();

            var inner = new Exception();
            inner.Data["SqlState"] = "23505";
            var dupEx = new DbUpdateException("duplicate", inner);

            _mockIdempotencyKeyRepository
                .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dupEx);

            // Act
            await _handler.Process(correspondenceId, CancellationToken.None);

            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Never);

            // Only the main notification delivery check should be scheduled (once)
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job =>
                    job.Type == typeof(CheckNotificationDeliveryHandler) &&
                    job.Method.Name == nameof(CheckNotificationDeliveryHandler.Process)),
                It.Is<IState>(state => state is ScheduledState)),
                Times.Once);
        }
    }
}


