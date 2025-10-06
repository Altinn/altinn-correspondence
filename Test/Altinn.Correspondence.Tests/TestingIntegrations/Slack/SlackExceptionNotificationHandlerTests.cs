using Altinn.Correspondence.Integrations.Slack;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Slack.Webhooks;

namespace Altinn.Correspondence.Tests.TestingIntegrations.Slack
{
    public class SlackExceptionNotificationHandlerTests
    {
        [Fact]
        public async Task TryHandleAsync_OnMigrationPath_DoesNotSendSlackMesssage()
        {
            // Arrange
            var logger = new Mock<ILogger<SlackExceptionNotificationHandler>>();
            var slackClient = new Mock<ISlackClient>(MockBehavior.Strict);
            var problemDetailsService = new Mock<IProblemDetailsService>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var handler = new SlackExceptionNotificationHandler(
                logger.Object,
                slackClient.Object,
                problemDetailsService.Object,
                hostEnvironment.Object,
                slackSettings);

            var context = new DefaultHttpContext();
            context.Request.Path = "/correspondence/api/v1/migration/correspondence/syncStatusEvent";

            var exception = new Exception("test");

            // Act
            var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

            // Assert
            Assert.True(handled);
            slackClient.Verify(x => x.PostAsync(It.IsAny<SlackMessage>()), Times.Never);
        }

        [Fact]
        public async Task TryHandleAsync_OnNonMigrationPath_SendsSlackMessage()
        {
            // Arrange
            var logger = new Mock<ILogger<SlackExceptionNotificationHandler>>();
            var slackClient = new Mock<ISlackClient>(MockBehavior.Strict);
            var problemDetailsService = new Mock<IProblemDetailsService>();
            var hostEnvironment = new Mock<IHostEnvironment>();
            var slackSettings = new SlackSettings(hostEnvironment.Object);

            var handler = new SlackExceptionNotificationHandler(
                logger.Object,
                slackClient.Object,
                problemDetailsService.Object,
                hostEnvironment.Object,
                slackSettings);

            var context = new DefaultHttpContext();
            context.Request.Path = "/correspondence/api/v1/correspondence/";

            var exception = new Exception("test");

            // Act
            var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

            // Assert
            Assert.True(handled);
            slackClient.Verify(x => x.PostAsync(It.IsAny<SlackMessage>()), Times.Once);
        }
    }
}


