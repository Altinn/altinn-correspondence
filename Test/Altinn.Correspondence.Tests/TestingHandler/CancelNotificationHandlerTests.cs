using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Dialogporten;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class CancelNotificationHandlerTests {
        


        [Fact]
        public async Task CancelNotificationHandler_SendsSlackNotification_WhenCancellationJobFailsWithMaximumRetries()
        {
            // Arrange
            var correspondence = CorrespondenceBuilder.CorrespondenceEntityWithNotifications();
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondence.Id,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync(correspondence);

            var notificationEntities = correspondence.Notifications;
            notificationEntities.ForEach(notification =>
            {
                notification.RequestedSendTime = correspondence.RequestedPublishTime.AddMinutes(1); // Set requested send time to future
                notification.NotificationOrderId = null; // Invalidate notification order id
            });

            // Act
            try
            {
                var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
                await cancelNotificationHandler.CancelNotification(correspondence.Id, notificationEntities, retryAttempts: 10, fixedTimestamp, default);
            }
            catch
            {
                Console.WriteLine("Exception thrown");
            }
            // Assert
            slackClientMock.Verify(client => client.Post(It.IsAny<SlackMessage>()), Times.Once);
        }

        [Fact]
        public async Task CancelNotification_ThrowsException_WhenCorrespondenceIsNull()
        {
            // Arrange
            var correspondenceId = Guid.NewGuid();
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondenceId,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync((CorrespondenceEntity?)null);

            // Act & Assert
            var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
            var exception = await Assert.ThrowsAsync<Exception>(async () =>
                await cancelNotificationHandler.CancelNotification(correspondenceId, new List<CorrespondenceNotificationEntity>(), retryAttempts: 0, fixedTimestamp, default));

            Assert.Contains($"Correspondence with id: {correspondenceId} was not found", exception.Message);
        }

        [Fact]
        public async Task CancelNotification_ThrowsException_WhenCorrespondenceHasNoDialogportenDialogId()
        {
            // Arrange
            var correspondence = new CorrespondenceEntityBuilder()
                .WithId(Guid.NewGuid())
                .Build();
            
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondence.Id,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync(correspondence);

            // Act & Assert
            var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
            var exception = await Assert.ThrowsAsync<Exception>(async () =>
                await cancelNotificationHandler.CancelNotification(correspondence.Id, new List<CorrespondenceNotificationEntity>(), retryAttempts: 0, fixedTimestamp, default));

            Assert.Contains($"Correspondence with id: {correspondence.Id} has no DialogportenDialogId", exception.Message);
        }

        [Fact]
        public async Task CancelNotification_LogsWarning_WhenCorrespondenceHasFailedStatus()
        {
            // Arrange
            var correspondence = new CorrespondenceEntityBuilder()
                .WithId(Guid.NewGuid())
                .WithExternalReference(ReferenceType.DialogportenDialogId, "test-dialog-id")
                .WithStatus(CorrespondenceStatus.Failed)
                .Build();
            
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondence.Id,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync(correspondence);

            // Act
            var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
            await cancelNotificationHandler.CancelNotification(correspondence.Id, new List<CorrespondenceNotificationEntity>(), retryAttempts: 0, fixedTimestamp, default);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Correspondence with id: {correspondence.Id} has status Failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelNotification_LogsWarning_WhenCorrespondenceHasPurgedByAltinnStatus()
        {
            // Arrange
            var correspondence = new CorrespondenceEntityBuilder()
                .WithId(Guid.NewGuid())
                .WithExternalReference(ReferenceType.DialogportenDialogId, "test-dialog-id")
                .WithStatus(CorrespondenceStatus.PurgedByAltinn)
                .Build();
            
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondence.Id,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync(correspondence);

            // Act
            var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
            await cancelNotificationHandler.CancelNotification(correspondence.Id, new List<CorrespondenceNotificationEntity>(), retryAttempts: 0, fixedTimestamp, default);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Correspondence with id: {correspondence.Id} has status PurgedByAltinn")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelNotification_LogsWarning_WhenCorrespondenceHasPurgedByRecipientStatus()
        {
            // Arrange
            var correspondence = new CorrespondenceEntityBuilder()
                .WithId(Guid.NewGuid())
                .WithExternalReference(ReferenceType.DialogportenDialogId, "test-dialog-id")
                .WithStatus(CorrespondenceStatus.PurgedByRecipient)
                .Build();
            
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondence.Id,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync(correspondence);

            // Act
            var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
            await cancelNotificationHandler.CancelNotification(correspondence.Id, new List<CorrespondenceNotificationEntity>(), retryAttempts: 0, fixedTimestamp, default);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Correspondence with id: {correspondence.Id} has status PurgedByRecipient")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelNotification_ProceedsWithCancellation_WhenCorrespondenceIsValidWithDialogId()
        {
            // Arrange
            var correspondence = new CorrespondenceEntityBuilder()
                .WithId(Guid.NewGuid())
                .WithExternalReference(ReferenceType.DialogportenDialogId, "test-dialog-id")
                .WithStatus(CorrespondenceStatus.Published)
                .Build();

            // Add a valid notification with future send time
            var notification = new CorrespondenceNotificationEntity
            {
                Id = Guid.NewGuid(),
                CorrespondenceId = correspondence.Id,
                NotificationOrderId = Guid.NewGuid(),
                RequestedSendTime = DateTimeOffset.UtcNow.AddHours(1), // Future send time
                Created = DateTimeOffset.UtcNow,
                NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                NotificationChannel = NotificationChannel.Email
            };
            
            var loggerMock = new Mock<ILogger<CancelNotificationHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var altinnNotificationServiceMock = new Mock<IAltinnNotificationService>();
            var slackClientMock = new Mock<ISlackClient>();
            var backgroundJobClient = new Mock<IBackgroundJobClient>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var dialogportenService = new DialogportenDevService();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var cancelNotificationHandler = new CancelNotificationHandler(
                loggerMock.Object,
                correspondenceRepositoryMock.Object,
                altinnNotificationServiceMock.Object,
                slackClientMock.Object,
                backgroundJobClient.Object,
                hostEnvironment.Object,
                dialogportenService,
                slackSettings);

            correspondenceRepositoryMock
                .Setup(r => r.GetCorrespondenceById(
                    correspondence.Id,
                    true,
                    false,
                    false,
                    CancellationToken.None,
                    false))
                .ReturnsAsync(correspondence);

            // Act
            var fixedTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
            await cancelNotificationHandler.CancelNotification(correspondence.Id, new List<CorrespondenceNotificationEntity> { notification }, retryAttempts: 0, fixedTimestamp, default);

            // Assert
            // Verify no warning logs for status or dialog ID issues
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("has status") || v.ToString()!.Contains("has no DialogportenDialogId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
    }
}