using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.Dialogporten;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class CancelNotificationHandlerTests
    {
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
    }
}
