using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SyncCorrespondenceNotificationEventHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceNotificationRepository> _correspondenceNotificationRepositoryMock;
        private readonly Mock<ILogger<SyncCorrespondenceNotificationEventHandler>> _loggerMock;
        private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock;
        private readonly Mock<ICorrespondenceDeleteEventRepository> _correspondenceDeleteRepositoryMock;
        private readonly Mock<ICorrespondenceForwardingEventRepository> _forwardingEventRepositoryMock;
        private readonly Mock<IAltinnRegisterService> _altinnRegisterServiceMock;
        private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock;
        private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock;
        private readonly Mock<IDialogportenService> _dialogportenServiceMock;
        private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
        private readonly Mock<ILogger<CorrespondenceMigrationEventHelper>> _eventHelperLoggerMock;
        private readonly SyncCorrespondenceNotificationEventHandler _handler;

        public SyncCorrespondenceNotificationEventHandlerTests()
        {
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceNotificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
            _loggerMock = new Mock<ILogger<SyncCorrespondenceNotificationEventHandler>>();
            
            // Setup mocks for CorrespondenceMigrationEventHelper dependencies
            _correspondenceStatusRepositoryMock = new Mock<ICorrespondenceStatusRepository>();
            _correspondenceDeleteRepositoryMock = new Mock<ICorrespondenceDeleteEventRepository>();
            _forwardingEventRepositoryMock = new Mock<ICorrespondenceForwardingEventRepository>();
            _altinnRegisterServiceMock = new Mock<IAltinnRegisterService>();
            _attachmentRepositoryMock = new Mock<IAttachmentRepository>();
            _attachmentStatusRepositoryMock = new Mock<IAttachmentStatusRepository>();
            _dialogportenServiceMock = new Mock<IDialogportenService>();
            _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
            _eventHelperLoggerMock = new Mock<ILogger<CorrespondenceMigrationEventHelper>>();
            
            var purgeCorrespondenceHelper = new PurgeCorrespondenceHelper(
                _attachmentRepositoryMock.Object,
                _attachmentStatusRepositoryMock.Object,
                _correspondenceStatusRepositoryMock.Object,
                _backgroundJobClientMock.Object,
                _dialogportenServiceMock.Object,
                _correspondenceRepositoryMock.Object);

            var correspondenceMigrationEventHelper = new CorrespondenceMigrationEventHelper(
                _correspondenceStatusRepositoryMock.Object,
                _correspondenceDeleteRepositoryMock.Object,
                _correspondenceNotificationRepositoryMock.Object,
                _forwardingEventRepositoryMock.Object,
                _altinnRegisterServiceMock.Object,
                purgeCorrespondenceHelper,
                _backgroundJobClientMock.Object,
                _eventHelperLoggerMock.Object);

            _handler = new SyncCorrespondenceNotificationEventHandler(
                _correspondenceRepositoryMock.Object,
                correspondenceMigrationEventHelper,
                _loggerMock.Object);
        }

        [Fact]
        public async Task Process_NotAvailable_NewReminderNotification_AddedOK()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)                
                .WithAltinn2CorrespondenceId(12345)
                .WithSingleAltinn2Notification(1,"testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 7, 12, 0, 0), false)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceNotificationEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceNotificationEntity>
                {
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId =2,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "testemail@altinn.no",
                        NotificationChannel = NotificationChannel.Email,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 14)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = true
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotificationForSync(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotificationForSync(It.Is<CorrespondenceNotificationEntity>(n => 
                n.Altinn2NotificationId == 2 && n.SyncedFromAltinn2 != null && n.CorrespondenceId == correspondenceId), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_Available_NewReminderNotification_AddedOK()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithSingleAltinn2Notification(1, "testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 7, 12, 0, 0), false)
                .WithIsMigrating(false) // Available in Altinn 3 APIs
                .WithDialogId("dialog-12345")
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceNotificationEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceNotificationEntity>
                {
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId =2,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "testemail@altinn.no",
                        NotificationChannel = NotificationChannel.Email,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 14)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = true
                    }
                }
            };


            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotificationForSync(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotificationForSync(It.Is<CorrespondenceNotificationEntity>(n =>
                n.Altinn2NotificationId == 2 && n.SyncedFromAltinn2 != null && n.CorrespondenceId == correspondenceId), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_NotAvailable_MultipleNewNotifications_AddedOK()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithSingleAltinn2Notification(1, "testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 7, 12, 0, 0), false)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceNotificationEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceNotificationEntity>
                {
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId = 2,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "testemail@altinn.no",
                        NotificationChannel = NotificationChannel.Email,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 10)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = true
                    },
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId = 3,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "+4790000000",
                        NotificationChannel = NotificationChannel.Sms,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 14)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = true
                    }
                }
            };


            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotificationForSync(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotificationForSync(It.Is<CorrespondenceNotificationEntity>(n =>
                n.Altinn2NotificationId == 2 && n.SyncedFromAltinn2 != null && n.CorrespondenceId == correspondenceId), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotificationForSync(It.Is<CorrespondenceNotificationEntity>(n =>
                n.Altinn2NotificationId == 3 && n.SyncedFromAltinn2 != null && n.CorrespondenceId == correspondenceId), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_NotAvailable_DuplicateNotification_NotAdded()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithSingleAltinn2Notification(1, "testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 7, 12, 0, 0), false)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceNotificationEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceNotificationEntity>
                {
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId = 1,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "testemail@altinn.no",
                        NotificationChannel = NotificationChannel.Email,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 7, 12, 0, 0)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = false
                    }
                }
            };


            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotificationForSync(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            // Verify that no new notification was added
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_NotAvailable_DuplicateNotificationWithOffset_NotAdded()
        {
            // Arrange            
            var partyUuid = Guid.NewGuid();

            var correspondence = new CorrespondenceEntityBuilder()
                .WithStatus(CorrespondenceStatus.Published)
                .WithAltinn2CorrespondenceId(12345)
                .WithSingleAltinn2Notification(1, "testemail@altinn.no", NotificationChannel.Email, new DateTime(2024, 1, 7), new DateTime(2024, 1, 7, 12, 0, 0), false)
                .WithIsMigrating(true) // Not available in Altinn 3 APIs
                .Build();
            var correspondenceId = correspondence.Id;

            var request = new SyncCorrespondenceNotificationEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceNotificationEntity>
                {
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId = 1,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "testemail@altinn.no",
                        NotificationChannel = NotificationChannel.Email,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 7, 12, 0, 0, 250)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = false
                    }
                }
            };


            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotificationForSync(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            // Verify that no new notification was added
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Process_CorrespondenceNotFound_ReturnError()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var request = new SyncCorrespondenceNotificationEventRequest
            {
                CorrespondenceId = correspondenceId,
                SyncedEvents = new List<CorrespondenceNotificationEntity>
                {
                    new CorrespondenceNotificationEntity
                    {
                        Altinn2NotificationId = 1,
                        NotificationTemplate = NotificationTemplate.Altinn2Message,
                        NotificationAddress = "testemail@altinn.no",
                        NotificationChannel = NotificationChannel.Email,
                        NotificationSent = new DateTimeOffset(new DateTime(2024, 1, 7, 12, 0, 0)),
                        Created = new DateTimeOffset(new DateTime(2024, 1, 7)),
                        IsReminder = false
                    }
                }
            };

            // Mock correspondence repository
            _correspondenceRepositoryMock
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CorrespondenceEntity?)null);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert Not Found Return
            Assert.False(result.IsT0);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, result.AsT1.StatusCode);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, CorrespondenceSyncType.NotificationEvents, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            // Verify that no new notification was added
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }
    }
} 