using Altinn.Correspondence.Integrations.OpenTelemetry;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Altinn.Correspondence.Tests.TestingFeature
{
    public class RequestProcessorFilterTests
    {
        /// <summary>
        /// Should trace non-HttpRequestIn activities regardless of settings
        /// </summary>
        [Fact]
        public void ShouldMarkAsRecordedForNonHttpRequestActivity()
        {
            // Arrange
            var dependencyFilterProcessor = new RequestFilterProcessor();

            var activity = new System.Diagnostics.Activity("Postgres");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnStart(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }

        /// <summary>
        /// Should not throw exception if log outside of HttpContext
        /// </summary>
        [Fact]
        public void ShouldNotFailOutsideOfHttpContext()
        {
            // Arrange
            var dependencyFilterProcessor = new RequestFilterProcessor(null);

            var activity = new System.Diagnostics.Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            try
            {
                dependencyFilterProcessor.OnStart(activity);
            }
            catch (Exception e)
            {
                // Assert
                Assert.False(true, $"Exception thrown: {e.Message}");
            }

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }

        /// <summary>
        /// Should trace regular request activities
        /// </summary>
        [Fact]
        public void ShouldMarkAsRecordedForNormalRequest()
        {
            // Arrange
            Mock<IHttpContextAccessor> httpContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/correspondence/api/v1/correspondence";
            httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);

            var dependencyFilterProcessor = new RequestFilterProcessor(httpContextAccessor.Object);

            var activity = new System.Diagnostics.Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnStart(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }

        /// <summary>
        /// Should exclude activities even if url contains query parameters
        /// </summary>
        [Fact]
        public void ShouldMarkAsNoneWithQueryParameters()
        {
            // Arrange
            Mock<IHttpContextAccessor> httpContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/health?resourceId=123";
            httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);

            var dependencyFilterProcessor = new RequestFilterProcessor(httpContextAccessor.Object);

            var activity = new System.Diagnostics.Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnStart(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.None, activity.ActivityTraceFlags);
        }

        /// <summary>
        /// Should exclude health check calls
        /// </summary>
        [Fact]
        public void ShouldMarkAsNoneForHealth()
        {
            // Arrange
            Mock<IHttpContextAccessor> httpContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/health";
            httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);

            var dependencyFilterProcessor = new RequestFilterProcessor(httpContextAccessor.Object);

            var activity = new System.Diagnostics.Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnStart(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.None, activity.ActivityTraceFlags);
        }

        /// <summary>
        /// Should exclude health check calls
        /// </summary>
        [Fact]
        public void ShouldMarkAsNoneForHealthz()
        {
            // Arrange
            Mock<IHttpContextAccessor> httpContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/healthz";
            httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(httpContext);

            var dependencyFilterProcessor = new RequestFilterProcessor(httpContextAccessor.Object);

            var activity = new System.Diagnostics.Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnStart(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.None, activity.ActivityTraceFlags);
        }
    }
}
