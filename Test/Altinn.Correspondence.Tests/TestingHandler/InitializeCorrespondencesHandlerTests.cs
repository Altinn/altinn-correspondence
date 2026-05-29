using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class InitializeCorrespondencesHandlerTests
{
    [Fact]
    public async Task ScheduleTransmissionAndPublishJobs_WithPastRequestedPublishTime_SchedulesTransmissionImmediately()
    {
        var correspondenceId = Guid.NewGuid();
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var correspondenceRepository = new Mock<ICorrespondenceRepository>();

        hangfireBackgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("transmission-job-id");

        correspondenceRepository
            .Setup(x => x.AreAllAttachmentsPublished(correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hangfireScheduleHelper = new HangfireScheduleHelper(
            hangfireBackgroundJobClient.Object,
            Mock.Of<IHybridCacheWrapper>(),
            correspondenceRepository.Object,
            NullLogger<HangfireScheduleHelper>.Instance);

        var handler = new InitializeCorrespondencesHandler(
            null!,
            correspondenceRepository.Object,
            hangfireBackgroundJobClient.Object,
            null!,
            null!,
            hangfireScheduleHelper,
            null!,
            null!,
            null!,
            NullLogger<InitializeCorrespondencesHandler>.Instance);

        await handler.ScheduleTransmissionAndPublishJobs(
            correspondenceId,
            attachmentsCount: 0,
            requestedPublishTime: DateTimeOffset.MinValue,
            CancellationToken.None);

        hangfireBackgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(InitializeCorrespondencesHandler.CreateDialogportenTransmission)),
            It.Is<IState>(state => IsImmediateScheduledState(state))),
            Times.Once);

        hangfireBackgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(HangfireScheduleHelper.SchedulePublishAtPublishTime)),
            It.Is<IState>(state => state is AwaitingState)),
            Times.Once);
    }

    private static bool IsImmediateScheduledState(IState state)
    {
        return state is ScheduledState scheduledState &&
            scheduledState.EnqueueAt > DateTime.UtcNow.AddMinutes(-1);
    }
}
