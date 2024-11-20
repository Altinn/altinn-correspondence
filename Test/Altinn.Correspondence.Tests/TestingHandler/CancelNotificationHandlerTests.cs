using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;

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

            var cancelNotificationHandler = new CancelNotificationHandler(loggerMock.Object, correspondenceRepositoryMock.Object, altinnNotificationServiceMock.Object, slackClientMock.Object, backgroundJobClient.Object, hostEnvironment.Object);
            var notificationEntities = correspondence.Notifications;
            notificationEntities.ForEach(notification =>
            {
                notification.RequestedSendTime = correspondence.RequestedPublishTime.AddMinutes(1); // Set requested send time to future
                notification.NotificationOrderId = null; // Invalidate notification order id
            });

            // Act
            try
            {
                await cancelNotificationHandler.CancelNotification(Guid.Empty, notificationEntities, retryAttempts: 10, default);
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
