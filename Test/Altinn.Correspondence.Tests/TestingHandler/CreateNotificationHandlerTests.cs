using Moq;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Application.CreateNotification;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Caching;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class CreateNotificationHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
        private readonly Mock<INotificationTemplateRepository> _mockNotificationTemplateRepository;
        private readonly Mock<IAltinnNotificationService> _mockAltinnNotificationService;
        private readonly Mock<IAltinnRegisterService> _mockAltinnRegisterService;
        private readonly Mock<ICorrespondenceNotificationRepository> _mockCorrespondenceNotificationRepository;
        private readonly Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<IHybridCacheWrapper> _mockHybridCacheWrapper;
        private readonly Mock<ILogger<HangfireScheduleHelper>> _mockHangfireLogger;
        private readonly HangfireScheduleHelper _hangfireScheduleHelper;
        private readonly Mock<IOptions<GeneralSettings>> _mockGeneralSettings;
        private readonly Mock<ILogger<CreateNotificationHandler>> _mockLogger;
        private readonly CreateNotificationHandler _handler;

        public CreateNotificationHandlerTests()
        {
            _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
            _mockNotificationTemplateRepository = new Mock<INotificationTemplateRepository>();
            _mockAltinnNotificationService = new Mock<IAltinnNotificationService>();
            _mockAltinnRegisterService = new Mock<IAltinnRegisterService>();
            _mockCorrespondenceNotificationRepository = new Mock<ICorrespondenceNotificationRepository>();
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockHybridCacheWrapper = new Mock<IHybridCacheWrapper>();
            _mockHangfireLogger = new Mock<ILogger<HangfireScheduleHelper>>();
            _mockGeneralSettings = new Mock<IOptions<GeneralSettings>>();
            _mockLogger = new Mock<ILogger<CreateNotificationHandler>>();

            _mockGeneralSettings.Setup(x => x.Value).Returns(new GeneralSettings 
            { 
                CorrespondenceBaseUrl = "https://test.altinn.no" 
            });
            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

            // Create a real instance of HangfireScheduleHelper with all required dependencies
            _hangfireScheduleHelper = new HangfireScheduleHelper(
                _mockBackgroundJobClient.Object,
                _mockHybridCacheWrapper.Object,
                _mockCorrespondenceRepository.Object,
                _mockHangfireLogger.Object);

            _handler = new CreateNotificationHandler(
                _mockAltinnNotificationService.Object,
                _mockAltinnRegisterService.Object,
                _mockCorrespondenceRepository.Object,
                _mockCorrespondenceNotificationRepository.Object,
                _mockNotificationTemplateRepository.Object,
                _mockBackgroundJobClient.Object,
                _mockHostEnvironment.Object,
                _hangfireScheduleHelper,
                _mockGeneralSettings.Object,
                _mockLogger.Object);
        }

        private (CreateNotificationRequest request, CorrespondenceEntity correspondence, NotificationTemplateEntity template, NotificationOrderRequestResponseV2 response) SetupTestData(
            DateTimeOffset requestedPublishTime,
            string environment = "Development")
        {
            var correspondenceId = Guid.NewGuid();
            var notificationOrderId = Guid.NewGuid();
            var shipmentId = Guid.NewGuid();
            var reminderShipmentId = Guid.NewGuid();

            _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns(environment);

            var notificationRequest = new CreateNotificationRequest
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
                Recipient = "0192:991825827", // Valid Norwegian organization number with prefix
                RequestedPublishTime = requestedPublishTime,
                Sender = "sender",
                Statuses = new List<CorrespondenceStatusEntity>(),
                Created = DateTimeOffset.UtcNow,
                ExternalReferences = new List<ExternalReferenceEntity>
                {
                    new ExternalReferenceEntity
                    {
                        ReferenceType = ReferenceType.DialogportenDialogId,
                        ReferenceValue = "12345"
                    }
                }
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

            var expectedResponse = new NotificationOrderRequestResponseV2
            {
                NotificationOrderId = notificationOrderId,
                Notification = new NotificationResponseV2
                {
                    ShipmentId = shipmentId,
                    SendersReference = "ref1",
                    Reminders = new List<ReminderResponse>
                    {
                        new()
                        {
                            ShipmentId = reminderShipmentId,
                            SendersReference = "ref1"
                        }
                    }
                }
            };

            _mockCorrespondenceRepository.Setup(x => x.GetCorrespondenceById(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), CancellationToken.None, false))
                .ReturnsAsync(correspondence);

            _mockNotificationTemplateRepository.Setup(x => x.GetNotificationTemplates(It.IsAny<NotificationTemplate>(), CancellationToken.None, It.IsAny<string>()))
                .ReturnsAsync(new List<NotificationTemplateEntity> { template });

            _mockAltinnNotificationService.Setup(x => x.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), CancellationToken.None))
                .ReturnsAsync(expectedResponse);

            return (notificationRequest, correspondence, template, expectedResponse);
        }

        [Fact]
        public async Task Process_ShouldStoreCorrectFieldsInDatabase_WhenNotificationIsCreated()
        {
            // Arrange
            var testStartTime = DateTimeOffset.UtcNow;
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10); // Set a future publish time
            var (notificationRequest, correspondence, template, expectedResponse) = SetupTestData(requestedPublishTime);

            // Act
            await _handler.Process(notificationRequest, CancellationToken.None);

            // Calculate expected times
            var expectedMainNotificationTime = requestedPublishTime.UtcDateTime.AddMinutes(5);
            var expectedReminderTime = expectedMainNotificationTime.AddDays(1); // 1 day in Development environment

            // Assert
            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n => 
                n.NotificationOrderId == expectedResponse.NotificationOrderId &&
                n.ShipmentId == expectedResponse.Notification.ShipmentId &&
                n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage &&
                n.NotificationChannel == NotificationChannel.EmailPreferred &&
                n.CorrespondenceId == correspondence.Id &&
                !n.IsReminder &&
                n.Created >= testStartTime &&
                n.RequestedSendTime == expectedMainNotificationTime), CancellationToken.None), Times.Once);

            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n => 
                n.NotificationOrderId == expectedResponse.NotificationOrderId &&
                n.ShipmentId == expectedResponse.Notification.Reminders[0].ShipmentId &&
                n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage &&
                n.NotificationChannel == NotificationChannel.SmsPreferred &&
                n.CorrespondenceId == correspondence.Id &&
                n.IsReminder &&
                n.Created >= testStartTime &&
                n.RequestedSendTime == expectedReminderTime), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Process_ShouldUseCurrentTimePlusFiveMinutes_WhenPublishTimeIsInPast_AndInProduction()
        {
            // Arrange
            var testStartTime = DateTimeOffset.UtcNow;
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(-10); // Set a past publish time
            var (notificationRequest, correspondence, template, expectedResponse) = SetupTestData(requestedPublishTime, "Production");

            // Calculate expected times before running the handler
            var expectedMainNotificationTime = DateTime.UtcNow.AddMinutes(5);
            var expectedReminderTime = expectedMainNotificationTime.AddDays(7); // 7 days in Production environment

            // Act
            await _handler.Process(notificationRequest, CancellationToken.None);

            // Assert
            // Verify main notification was added (should be called once for the default recipient)
            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n => 
                n.NotificationOrderId == expectedResponse.NotificationOrderId &&
                n.ShipmentId == expectedResponse.Notification.ShipmentId &&
                n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage &&
                n.NotificationChannel == NotificationChannel.EmailPreferred &&
                n.CorrespondenceId == correspondence.Id &&
                !n.IsReminder &&
                n.Created >= testStartTime &&
                n.RequestedSendTime >= expectedMainNotificationTime.AddSeconds(-20) && // Allow 20 seconds difference
                n.RequestedSendTime <= expectedMainNotificationTime.AddSeconds(20)), CancellationToken.None), Times.Once);

            // Verify reminder notification was added (should be called once for the default recipient)
            _mockCorrespondenceNotificationRepository.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n => 
                n.NotificationOrderId == expectedResponse.NotificationOrderId &&
                n.ShipmentId == expectedResponse.Notification.Reminders[0].ShipmentId &&
                n.NotificationTemplate == NotificationTemplate.GenericAltinnMessage &&
                n.NotificationChannel == NotificationChannel.SmsPreferred &&
                n.CorrespondenceId == correspondence.Id &&
                n.IsReminder &&
                n.Created >= testStartTime &&
                n.RequestedSendTime >= expectedReminderTime.AddSeconds(-20) && // Allow 20 seconds difference
                n.RequestedSendTime <= expectedReminderTime.AddSeconds(20)), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Process_ShouldSetConditionEndpointForV2Reminders_WhenSendReminderIsTrue()
        {
            // Arrange
            var requestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var (notificationRequest, correspondence, template, expectedResponse) = SetupTestData(requestedPublishTime);

            // Act
            await _handler.Process(notificationRequest, CancellationToken.None);

            // Assert
            // Verify that CreateNotificationV2 was called with a reminder that has a condition endpoint
            _mockAltinnNotificationService.Verify(x => x.CreateNotificationV2(It.Is<NotificationOrderRequestV2>(req => 
                req.Reminders != null &&
                req.Reminders.Count == 1 &&
                req.Reminders[0].ConditionEndpoint != null &&
                req.Reminders[0].ConditionEndpoint.Contains($"/correspondence/api/v1/correspondence/{correspondence.Id}/notification/check") &&
                req.Reminders[0].SendersReference == correspondence.SendersReference),
                CancellationToken.None), Times.Once);
        }
    }
} 