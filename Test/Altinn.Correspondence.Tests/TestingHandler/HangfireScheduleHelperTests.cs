using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class HangfireScheduleHelperTests
{
    private readonly Guid _correspondenceId = Guid.NewGuid();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly Mock<IHybridCacheWrapper> _hybridCacheWrapper = new();
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepository = new();
    private readonly HangfireScheduleHelper _helper;

    public HangfireScheduleHelperTests()
    {
        _backgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns<Job, IState>((job, _) => job.Method.Name);
        _correspondenceRepository
            .Setup(x => x.AreAllAttachmentsPublished(_correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _correspondenceRepository
            .Setup(x => x.GetCorrespondenceById(_correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(CreateCorrespondence());

        _helper = new HangfireScheduleHelper(
            _backgroundJobClient.Object,
            _hybridCacheWrapper.Object,
            _correspondenceRepository.Object,
            NullLogger<HangfireScheduleHelper>.Instance);
    }

    [Fact]
    public async Task SchedulePublishAfterDialogCreated_ShouldContinuePublishFromDialogJob_WhenDialogJobIdIsCached()
    {
        // Arrange
        SetupCachedDialogJobId("dialog-job-id");

        // Act
        await _helper.SchedulePublishAfterDialogCreated(_correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(HangfireScheduleHelper.SchedulePublishAtPublishTime)),
            It.Is<IState>(state => state is AwaitingState
                && ((AwaitingState)state).ParentId == "dialog-job-id"
                && ((AwaitingState)state).Options == JobContinuationOptions.OnAnyFinishedState)),
            Times.Once);
    }

    [Fact]
    public async Task SchedulePublishAfterDialogCreated_ShouldSchedulePublish_WhenDialogJobIdIsMissingFromCacheAndDialogIsCreated()
    {
        // Arrange
        SetupCachedDialogJobId(null);
        _correspondenceRepository
            .Setup(x => x.GetCorrespondenceById(_correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(CreateCorrespondence(new ExternalReferenceEntity
            {
                ReferenceType = ReferenceType.DialogportenDialogId,
                ReferenceValue = "019abaa7-6dfd-7b82-9232-a34ba1e87fbd"
            }));

        // Act
        await _helper.SchedulePublishAfterDialogCreated(_correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(PublishCorrespondenceHandler)),
            It.Is<IState>(state => state is ScheduledState)),
            Times.Once);
    }

    [Fact]
    public async Task SchedulePublishAfterDialogCreated_ShouldNotScheduleAnyJob_WhenDialogJobIdIsMissingFromCacheAndDialogIsNotCreated()
    {
        // Arrange
        SetupCachedDialogJobId(null);

        // Act
        await _helper.SchedulePublishAfterDialogCreated(_correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task SchedulePublishAfterDialogCreated_ShouldNotScheduleAnyJob_WhenTransmissionIsNotCreated()
    {
        // Arrange
        SetupCachedDialogJobId(null);
        _correspondenceRepository
            .Setup(x => x.GetCorrespondenceById(_correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(CreateCorrespondence(
                new ExternalReferenceEntity
                {
                    ReferenceType = ReferenceType.DialogportenDialogId,
                    ReferenceValue = "019abaa7-6dfd-7b82-9232-a34ba1e87fbd"
                },
                new ExternalReferenceEntity
                {
                    ReferenceType = ReferenceType.DialogportenTransmissionType,
                    ReferenceValue = "Information"
                }));

        // Act
        await _helper.SchedulePublishAfterDialogCreated(_correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    public async Task SchedulePublishAfterDialogCreated_ShouldSchedulePublish_WhenTransmissionIsCreated()
    {
        // Arrange
        SetupCachedDialogJobId(null);
        _correspondenceRepository
            .Setup(x => x.GetCorrespondenceById(_correspondenceId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(CreateCorrespondence(
                new ExternalReferenceEntity
                {
                    ReferenceType = ReferenceType.DialogportenDialogId,
                    ReferenceValue = "019abaa7-6dfd-7b82-9232-a34ba1e87fbd"
                },
                new ExternalReferenceEntity
                {
                    ReferenceType = ReferenceType.DialogportenTransmissionType,
                    ReferenceValue = "Information"
                },
                new ExternalReferenceEntity
                {
                    ReferenceType = ReferenceType.DialogportenTransmissionId,
                    ReferenceValue = "transmission-123"
                }));

        // Act
        await _helper.SchedulePublishAfterDialogCreated(_correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(PublishCorrespondenceHandler)),
            It.Is<IState>(state => state is ScheduledState)),
            Times.Once);
    }

    [Fact]
    public async Task SchedulePublishAfterDialogCreated_ShouldNotScheduleAnyJob_WhenNotAllAttachmentsArePublished()
    {
        // Arrange
        SetupCachedDialogJobId("dialog-job-id");
        _correspondenceRepository
            .Setup(x => x.AreAllAttachmentsPublished(_correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _helper.SchedulePublishAfterDialogCreated(_correspondenceId, CancellationToken.None);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    private void SetupCachedDialogJobId(string? dialogJobId)
    {
        _hybridCacheWrapper
            .Setup(x => x.GetAsync<string?>(
                $"dialogJobId_{_correspondenceId}",
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dialogJobId);
    }

    private CorrespondenceEntity CreateCorrespondence(params ExternalReferenceEntity[] externalReferences)
    {
        return new CorrespondenceEntity
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
            Statuses = [],
            ExternalReferences = [.. externalReferences]
        };
    }
}
