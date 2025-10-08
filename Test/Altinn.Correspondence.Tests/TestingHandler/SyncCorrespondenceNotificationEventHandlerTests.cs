using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class SyncCorrespondenceNotificationEventHandlerTests
    {
        private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock;
        private readonly Mock<ICorrespondenceNotificationRepository> _correspondenceNotificationRepositoryMock;
        private readonly Mock<ILogger<SyncCorrespondenceNotificationEventHandler>> _loggerMock;
        private readonly SyncCorrespondenceNotificationEventHandler _handler;

        public SyncCorrespondenceNotificationEventHandlerTests()
        {
            _correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            _correspondenceNotificationRepositoryMock = new Mock<ICorrespondenceNotificationRepository>();
            _loggerMock = new Mock<ILogger<SyncCorrespondenceNotificationEventHandler>>();

            _handler = new SyncCorrespondenceNotificationEventHandler(
                _correspondenceRepositoryMock.Object,
                _correspondenceNotificationRepositoryMock.Object,
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
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n => 
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
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
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
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();

            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
                n.Altinn2NotificationId == 2 && n.SyncedFromAltinn2 != null && n.CorrespondenceId == correspondenceId), It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceNotificationRepositoryMock.Verify(x => x.AddNotification(It.Is<CorrespondenceNotificationEntity>(n =>
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
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()), Times.Once);
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
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondence);
            _correspondenceNotificationRepositoryMock
                .Setup(x => x.AddNotification(It.IsAny<CorrespondenceNotificationEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(correspondenceId);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert OK Return
            Assert.True(result.IsT0);
            Assert.Equal(correspondenceId, result.AsT0);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()), Times.Once);
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
                .Setup(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CorrespondenceEntity?)null);

            // Act
            var result = await _handler.Process(request, null, CancellationToken.None);

            // Assert Not Found Return
            Assert.False(result.IsT0);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, result.AsT1.StatusCode);

            // Verify correct calls to Correspondence repository
            _correspondenceRepositoryMock.Verify(x => x.GetCorrespondenceByIdForSync(correspondenceId, false, true, false, It.IsAny<CancellationToken>()), Times.Once);
            _correspondenceRepositoryMock.VerifyNoOtherCalls();
            // Verify that no new notification was added
            _correspondenceNotificationRepositoryMock.VerifyNoOtherCalls();
        }
    }
} 