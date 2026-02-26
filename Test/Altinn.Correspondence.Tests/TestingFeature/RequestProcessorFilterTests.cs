using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Integrations.OpenTelemetry;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Altinn.Correspondence.Tests.TestingFeature
{
    public class RequestProcessorFilterTests
    {
        /// <summary>
        /// Should trace all activities when disableTelemetryForMigration setting is false
        /// </summary>
        [Fact]
        public void ShouldMarkAsRecordedWhenNotDisableMigrationTelemetry()
        {
            // Arrange
            var dependencyFilterProcessor = new RequestFilterProcessor(new GeneralSettings { DisableTelemetryForMigration  = false });

            var activity = new System.Diagnostics.Activity("POST Migration");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnEnd(activity);

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
            var dependencyFilterProcessor = new RequestFilterProcessor(new GeneralSettings { DisableTelemetryForMigration = true }, null);

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
        /// Should trace all activities when disableTelemetryForMigration setting is false
        /// </summary>
        [Fact]
        public void ShouldMarkAsRecordenWhenOtherActivityAndNotDisableMigrationTelemetry()
        {
            // Arrange
            var dependencyFilterProcessor = new RequestFilterProcessor(new GeneralSettings { DisableTelemetryForMigration = false });

            var activity = new System.Diagnostics.Activity("Postgres");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnEnd(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }

        /// <summary>
        /// Should trace non-migration activities when disableTelemetryForMigration setting is false
        /// </summary>
        [Fact]
        public void ShouldMarkAsRecordenWhenOtherActivityAndDisableMigrationTelemetry()
        {
            // Arrange
            var dependencyFilterProcessor = new RequestFilterProcessor(new GeneralSettings { DisableTelemetryForMigration = true });

            var activity = new System.Diagnostics.Activity("Postgres");
            activity.ActivityTraceFlags = System.Diagnostics.ActivityTraceFlags.Recorded;

            // Act
            dependencyFilterProcessor.OnEnd(activity);

            // Assert
            Assert.Equal(System.Diagnostics.ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }
    }

}
