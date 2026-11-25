using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
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
            NullLogger<InitializeCorrespondencesHandler>.Instance,
            TestDbContextFactory.Create());

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
            It.Is<IState>(state => state is AwaitingState
                && ((AwaitingState)state).ParentId == "transmission-job-id"
                && ((AwaitingState)state).Options == JobContinuationOptions.OnAnyFinishedState)),
            Times.Once);
    }

    [Fact]
    public async Task ScheduleTransmissionAndPublishJobs_ShouldContinuePublishFromTransmissionJob_WhenNotAllAttachmentsArePublished()
    {
        // Arrange
        var correspondenceId = Guid.NewGuid();
        var hangfireBackgroundJobClient = new Mock<IBackgroundJobClient>();
        var correspondenceRepository = new Mock<ICorrespondenceRepository>();
        hangfireBackgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("transmission-job-id");
        correspondenceRepository
            .Setup(x => x.AreAllAttachmentsPublished(correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
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
            NullLogger<InitializeCorrespondencesHandler>.Instance,
            TestDbContextFactory.Create());

        // Act
        await handler.ScheduleTransmissionAndPublishJobs(
            correspondenceId,
            attachmentsCount: 1,
            requestedPublishTime: DateTimeOffset.MinValue,
            CancellationToken.None);

        // Assert
        hangfireBackgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(HangfireScheduleHelper.SchedulePublishAtPublishTime)),
            It.Is<IState>(state => state is AwaitingState
                && ((AwaitingState)state).ParentId == "transmission-job-id"
                && ((AwaitingState)state).Options == JobContinuationOptions.OnAnyFinishedState)),
            Times.Once);
    }

    [Theory]
    [InlineData(CorrespondenceStatus.Failed)]
    [InlineData(CorrespondenceStatus.PurgedByAltinn)]
    [InlineData(CorrespondenceStatus.PurgedByRecipient)]
    public async Task CreateDialogportenTransmission_ShouldNotCreateTransmission_WhenCorrespondenceHasFailedOrBeenPurged(CorrespondenceStatus status)
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithExternalReference(ReferenceType.DialogportenDialogId, "019abaa7-6dfd-7b82-9232-a34ba1e87fbd")
            .WithExternalReference(ReferenceType.DialogportenTransmissionType, "Information")
            .WithStatus(CorrespondenceStatus.Initialized)
            .WithStatus(status)
            .Build();
        var dialogportenService = new Mock<IDialogportenService>();
        var handler = CreateHandlerForTransmissionJob(correspondence, dialogportenService);

        // Act
        await handler.CreateDialogportenTransmission(correspondence.Id);

        // Assert
        dialogportenService.Verify(x => x.CreateDialogTransmission(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateDialogportenTransmission_ShouldNotCreateTransmission_WhenTransmissionIsAlreadyCreated()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithExternalReference(ReferenceType.DialogportenDialogId, "019abaa7-6dfd-7b82-9232-a34ba1e87fbd")
            .WithExternalReference(ReferenceType.DialogportenTransmissionType, "Information")
            .WithExternalReference(ReferenceType.DialogportenTransmissionId, "transmission-123")
            .WithStatus(CorrespondenceStatus.Initialized)
            .Build();
        var dialogportenService = new Mock<IDialogportenService>();
        var handler = CreateHandlerForTransmissionJob(correspondence, dialogportenService);

        // Act
        await handler.CreateDialogportenTransmission(correspondence.Id);

        // Assert
        dialogportenService.Verify(x => x.CreateDialogTransmission(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateDialogportenTransmission_ShouldCreateTransmission_WhenCorrespondenceIsAwaitingPublish()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithExternalReference(ReferenceType.DialogportenDialogId, "019abaa7-6dfd-7b82-9232-a34ba1e87fbd")
            .WithExternalReference(ReferenceType.DialogportenTransmissionType, "Information")
            .WithStatus(CorrespondenceStatus.Initialized)
            .Build();
        var dialogportenService = new Mock<IDialogportenService>();
        dialogportenService
            .Setup(x => x.CreateDialogTransmission(correspondence.Id))
            .ReturnsAsync("transmission-123");
        var handler = CreateHandlerForTransmissionJob(correspondence, dialogportenService);

        // Act
        await handler.CreateDialogportenTransmission(correspondence.Id);

        // Assert
        dialogportenService.Verify(x => x.CreateDialogTransmission(correspondence.Id), Times.Once);
    }

    private static InitializeCorrespondencesHandler CreateHandlerForTransmissionJob(CorrespondenceEntity correspondence, Mock<IDialogportenService> dialogportenService)
    {
        var correspondenceRepository = new Mock<ICorrespondenceRepository>();
        correspondenceRepository
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(correspondence);
        return new InitializeCorrespondencesHandler(
            null!,
            correspondenceRepository.Object,
            Mock.Of<IBackgroundJobClient>(),
            dialogportenService.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<InitializeCorrespondencesHandler>.Instance,
            TestDbContextFactory.Create());
    }

    private static bool IsImmediateScheduledState(IState state)
    {
        return state is ScheduledState scheduledState &&
            scheduledState.EnqueueAt > DateTime.UtcNow.AddMinutes(-1);
    }
}
