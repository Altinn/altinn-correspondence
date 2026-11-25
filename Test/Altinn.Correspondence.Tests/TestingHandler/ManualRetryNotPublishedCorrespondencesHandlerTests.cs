using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.ManualRetryNotPublishedCorrespondences;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class ManualRetryNotPublishedCorrespondencesHandlerTests
{
    private readonly Guid _correspondenceId = Guid.NewGuid();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepository = new();
    private readonly ManualRetryNotPublishedCorrespondencesHandler _handler;

    public ManualRetryNotPublishedCorrespondencesHandlerTests()
    {
        _backgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns<Job, IState>((job, _) => job.Method.Name);

        _handler = new ManualRetryNotPublishedCorrespondencesHandler(
            _backgroundJobClient.Object,
            _correspondenceRepository.Object,
            NullLogger<ManualRetryNotPublishedCorrespondencesHandler>.Instance);
    }

    [Theory]
    [InlineData(CorrespondenceStatus.Published)]
    [InlineData(CorrespondenceStatus.Failed)]
    [InlineData(CorrespondenceStatus.PurgedByAltinn)]
    [InlineData(CorrespondenceStatus.PurgedByRecipient)]
    public async Task Process_ShouldNotRetry_WhenCorrespondenceIsAlreadyPublishedFailedOrPurged(CorrespondenceStatus status)
    {
        // Arrange
        SetupCorrespondence(CorrespondenceStatus.Initialized, status);

        // Act
        var result = await _handler.Process(CreateRequest(), null, CancellationToken.None);

        // Assert
        Assert.Equal("Already published, failed or purged", result.AsT0.RetriedCorrespondenceIds[_correspondenceId]);
        _backgroundJobClient.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task Process_ShouldCreateDialogThenPublish_WhenCorrespondenceIsNotPublishedAndHasNoDialog()
    {
        // Arrange
        SetupCorrespondence(CorrespondenceStatus.Initialized);

        // Act
        await _handler.Process(CreateRequest(), null, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(InitializeCorrespondencesHandler.CreateDialogportenDialog)),
            It.Is<IState>(state => state is EnqueuedState)),
            Times.Once);
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(PublishCorrespondenceHandler)),
            It.Is<IState>(state => state is AwaitingState
                && ((AwaitingState)state).ParentId == nameof(InitializeCorrespondencesHandler.CreateDialogportenDialog)
                && ((AwaitingState)state).Options == JobContinuationOptions.OnAnyFinishedState)),
            Times.Once);
    }

    [Fact]
    public async Task Process_ShouldScheduleTransmissionAndPublish_WhenTransmissionIsNotCreated()
    {
        // Arrange
        SetupCorrespondence(
            [
                new ExternalReferenceEntity { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "019abaa7-6dfd-7b82-9232-a34ba1e87fbd" },
                new ExternalReferenceEntity { ReferenceType = ReferenceType.DialogportenTransmissionType, ReferenceValue = "Information" }
            ],
            [CorrespondenceStatus.Initialized]);

        // Act
        await _handler.Process(CreateRequest(), null, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(InitializeCorrespondencesHandler.ScheduleTransmissionAndPublishJobs)),
            It.Is<IState>(state => state is EnqueuedState)),
            Times.Once);
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(InitializeCorrespondencesHandler.CreateDialogportenTransmission)),
            It.IsAny<IState>()),
            Times.Never);
    }

    private ManualRetryNotPublishedCorrespondencesRequest CreateRequest()
    {
        return new ManualRetryNotPublishedCorrespondencesRequest { CorrespondenceIds = [_correspondenceId] };
    }

    private void SetupCorrespondence(params CorrespondenceStatus[] statuses)
    {
        SetupCorrespondence([], statuses);
    }

    private void SetupCorrespondence(List<ExternalReferenceEntity> externalReferences, CorrespondenceStatus[] statuses)
    {
        _correspondenceRepository
            .Setup(x => x.GetCorrespondenceById(_correspondenceId, true, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new CorrespondenceEntity
            {
                Id = _correspondenceId,
                Sender = "urn:altinn:organization:identifier-no:991825827",
                Recipient = "urn:altinn:organization:identifier-no:200000005",
                ResourceId = "resource-123",
                SendersReference = "ref-123",
                Content = new CorrespondenceContentEntity
                {
                    Language = "nb",
                    CorrespondenceId = _correspondenceId,
                    MessageTitle = "Test message title",
                    MessageSummary = "Test message summary",
                    MessageBody = "Test message body",
                    Attachments = []
                },
                RequestedPublishTime = DateTimeOffset.UtcNow,
                Created = DateTimeOffset.UtcNow,
                ExternalReferences = externalReferences,
                Statuses = [.. statuses.Select(status => new CorrespondenceStatusEntity
                {
                    CorrespondenceId = _correspondenceId,
                    Status = status,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = status.ToString()
                })]
            });
    }
}
