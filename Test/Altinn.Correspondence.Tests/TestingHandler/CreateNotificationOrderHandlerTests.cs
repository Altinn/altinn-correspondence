using Moq;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Application.CreateNotificationOrder;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class CreateNotificationOrderHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
        private readonly Mock<INotificationTemplateRepository> _mockNotificationTemplateRepository;
        private readonly Mock<IAltinnRegisterService> _mockAltinnRegisterService;
        private readonly Mock<ICorrespondenceNotificationRepository> _mockCorrespondenceNotificationRepository;
        private readonly Mock<IIdempotencyKeyRepository> _mockIdempotencyKeyRepository;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<IOptions<GeneralSettings>> _mockGeneralSettings;
        private readonly Mock<ILogger<CreateNotificationOrderHandler>> _mockLogger;
        private readonly CreateNotificationOrderHandler _handler;

        public CreateNotificationOrderHandlerTests()
        {
            _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
            _mockNotificationTemplateRepository = new Mock<INotificationTemplateRepository>();
            _mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
            _mockCorrespondenceNotificationRepository = new Mock<ICorrespondenceNotificationRepository>();
            _mockIdempotencyKeyRepository = new Mock<IIdempotencyKeyRepository>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockGeneralSettings = new Mock<IOptions<GeneralSettings>>();
            _mockLogger = new Mock<ILogger<CreateNotificationOrderHandler>>();

            _mockGeneralSettings.Setup(x => x.Value).Returns(new GeneralSettings
            {
                CorrespondenceBaseUrl = "https://test.altinn.no"
            });
            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Development");
            _mockAltinnRegisterService.Setup(x => x.LookUpName(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("Name");

            _handler = new CreateNotificationOrderHandler(
                _mockCorrespondenceRepository.Object,
                _mockAltinnRegisterService.Object,
                _mockNotificationTemplateRepository.Object,
                _mockCorrespondenceNotificationRepository.Object,
                _mockIdempotencyKeyRepository.Object,
                _mockHostEnvironment.Object,
                _mockGeneralSettings.Object,
                _mockLogger.Object);
        }

        private (CreateNotificationOrderRequest request, CorrespondenceEntity correspondence, NotificationTemplateEntity template) SetupOrderData(DateTimeOffset requestedPublishTime)
        {
            var correspondenceId = Guid.NewGuid();
            var request = new CreateNotificationOrderRequest
            {
                CorrespondenceId = correspondenceId,
                NotificationRequest = new NotificationRequest
                {
                    NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                    NotificationChannel = NotificationChannel.EmailPreferred,
                    SendReminder = true,
                    EmailSubject = "Test Subject",
                    EmailBody = "Test Body",
                    ReminderEmailSubject = "Reminder Subject",
                    ReminderEmailBody = "Reminder Body",
                    ReminderNotificationChannel = NotificationChannel.SmsPreferred
                },
                Language = "nb"
            };

            var correspondence = new CorrespondenceEntity
            {
                Id = correspondenceId,
                ResourceId = "resource1",
                SendersReference = "ref1",
                Recipient = "urn:altinn:person:identifier-no:08900499559",
                RequestedPublishTime = requestedPublishTime,
                Sender = "sender",
                Statuses = new List<CorrespondenceStatusEntity>(),
                Created = DateTimeOffset.UtcNow
            };

            var template = new NotificationTemplateEntity
            {
                Id = 1,
                Template = NotificationTemplate.GenericAltinnMessage,
                Language = "nb",
                EmailSubject = "Test Subject",
                EmailBody = "Test Body",
                SmsBody = "Test SMS",
                ReminderEmailSubject = "Reminder Subject",
                ReminderEmailBody = "Reminder Body",
                ReminderSmsBody = "Reminder SMS"
            };

            _mockCorrespondenceRepository
                .Setup(x => x.GetCorrespondenceById(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), CancellationToken.None, false))
                .ReturnsAsync(correspondence);

            _mockNotificationTemplateRepository
                .Setup(x => x.GetNotificationTemplates(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(new List<NotificationTemplateEntity> { template });

            return (request, correspondence, template);
        }

        [Fact]
        public async Task Process_ShouldPersistOrder_WithCorrectFields()
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);

            await _handler.Process(request, CancellationToken.None);

            var expectedMainTime = requestedPublishTime.UtcDateTime;
            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
                n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage &&
                n.NotificationChannel == NotificationChannel.EmailPreferred &&
                n.CorrespondenceId == correspondence.Id &&
                !n.IsReminder &&
                n.RequestedSendTime == expectedMainTime &&
                n.OrderRequest != null &&
                JsonSerializer.Deserialize<NotificationOrderRequestV2>(n.OrderRequest!, (JsonSerializerOptions?)null) != null
            ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Process_ShouldDeduplicateRecipients_WithSameIdentifier()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);

            request.NotificationRequest.OverrideRegisteredContactInformation = true;
            var nin = "26818099001";
            request.NotificationRequest.CustomRecipients =
            [
                new Recipient { NationalIdentityNumber = nin },
                new Recipient { NationalIdentityNumber = nin }
            ];

            // Act
            await _handler.Process(request, CancellationToken.None);

            // Assert: idempotency key and notification are each created only once
            _mockIdempotencyKeyRepository.Verify(
                x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _mockCorrespondenceNotificationRepository.Verify(
                x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Process_ShouldUseNow_WhenPublishTimeInPast_Production()
        {
            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(-10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);

            var before = DateTime.UtcNow.AddSeconds(-20);
            await _handler.Process(request, CancellationToken.None);
            var after = DateTime.UtcNow.AddSeconds(20);

            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
                n.RequestedSendTime >= before && n.RequestedSendTime <= after
            ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Process_ShouldSetConditionEndpoint_InOrderRequest_WhenSendReminderIsTrue()
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Reminders);
            Assert.True(deserialized.Reminders!.Count >= 1);
            Assert.NotNull(deserialized.Reminders![0].ConditionEndpoint);
            Assert.Contains($"/correspondence/api/v1/correspondence/{correspondence.Id}/notification/check", deserialized.Reminders![0].ConditionEndpoint!);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task Process_ShouldPassIgnoreReservation_ToRecipientPerson(bool? ignoreReservation)
        {
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, correspondence, _) = SetupOrderData(requestedPublishTime);
            correspondence.IgnoreReservation = ignoreReservation;

            CorrespondenceNotificationEntity? captured = null;
            _mockCorrespondenceNotificationRepository
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .Callback<CorrespondenceNotificationEntity, CancellationToken>((n, _) => captured = n)
                .ReturnsAsync(Guid.NewGuid());

            await _handler.Process(request, CancellationToken.None);

            Assert.NotNull(captured);
            var deserialized = JsonSerializer.Deserialize<NotificationOrderRequestV2>(captured!.OrderRequest!);
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized!.Recipient.RecipientPerson);
            Assert.Equal(ignoreReservation, deserialized!.Recipient.RecipientPerson!.IgnoreReservation);
        }

        [Fact]
        public async Task Process_ShouldSkipPersist_WhenIdempotencyKeyExists()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (request, _, _) = SetupOrderData(requestedPublishTime);

            var inner = new Exception();
            inner.Data["SqlState"] = "23505";
            var dupEx = new DbUpdateException("duplicate", inner);

            _mockIdempotencyKeyRepository
                .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dupEx);

            // Act
            await _handler.Process(request, CancellationToken.None);

            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}