using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.CleanupBruksmonster;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CleanupBruksmonsterHandlerTests
{
    private const string AllowedResourceId = "correspondence-bruksmonstertester-ressurs";

    private static CleanupBruksmonsterHandler CreateHandler(
        out Mock<IBackgroundJobClient> backgroundJobClientMock,
        out Mock<IDialogportenService> dialogportenServiceMock,
        out Mock<ICorrespondenceRepository> correspondenceRepositoryMock,
        out Mock<IIdempotencyKeyRepository> idempotencyKeyRepositoryMock,
        out Mock<IAttachmentRepository> attachmentRepositoryMock)
    {
        backgroundJobClientMock = new Mock<IBackgroundJobClient>(MockBehavior.Strict);
        dialogportenServiceMock = new Mock<IDialogportenService>(MockBehavior.Strict);
        correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>(MockBehavior.Strict);
        idempotencyKeyRepositoryMock = new Mock<IIdempotencyKeyRepository>(MockBehavior.Strict);
        attachmentRepositoryMock = new Mock<IAttachmentRepository>(MockBehavior.Strict);

        var logger = Mock.Of<ILogger<CleanupBruksmonsterHandler>>();

        return new CleanupBruksmonsterHandler(
            backgroundJobClientMock.Object,
            logger,
            dialogportenServiceMock.Object,
            correspondenceRepositoryMock.Object,
            idempotencyKeyRepositoryMock.Object,
            attachmentRepositoryMock.Object
        );
    }

    [Fact]
    public async Task Process_WhenValid_SchedulesJobs_AndTargetsOnlyConfiguredResource()
    {
        // Arrange
        var handler = CreateHandler(
            out var bgMock,
            out var dialogMock,
            out var corrRepoMock,
            out var idRepoMock,
            out var attRepoMock);

        var correspondenceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var attachmentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        corrRepoMock
            .Setup(m => m.GetCorrespondenceIdsByResourceId(AllowedResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondenceIds);
        attRepoMock
            .Setup(m => m.GetAttachmentIdsOnResource(AllowedResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachmentIds);

        var createdJobs = new List<(Job job, IState state)>();
        bgMock
            .Setup(m => m.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, state) => createdJobs.Add((job, state)))
            .Returns(() => $"job-id-{createdJobs.Count}");

        // Act
        var result = await handler.Process(user: null, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0);
        var response = result.AsT0;
        Assert.Equal(AllowedResourceId, response.ResourceId);
        Assert.Equal(correspondenceIds.Count, response.CorrespondencesFound);
        Assert.Equal(attachmentIds.Count, response.AttachmentsFound);

        corrRepoMock.Verify(m => m.GetCorrespondenceIdsByResourceId(AllowedResourceId, It.IsAny<CancellationToken>()), Times.Once);
        attRepoMock.Verify(m => m.GetAttachmentIdsOnResource(AllowedResourceId, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(2, createdJobs.Count);
        Assert.Equal("PurgeCorrespondenceDialogs", createdJobs[0].job.Method.Name);
        var enqCorrIds = Assert.IsAssignableFrom<IEnumerable<Guid>>(createdJobs[0].job.Args[0]!);
        Assert.Equal(correspondenceIds, enqCorrIds);
        Assert.Equal("PurgeCorrespondences", createdJobs[1].job.Method.Name);
        var contCorrIds = Assert.IsAssignableFrom<IEnumerable<Guid>>(createdJobs[1].job.Args[0]!);
        var contAttIds = Assert.IsAssignableFrom<IEnumerable<Guid>>(createdJobs[1].job.Args[1]!);
        var contResourceId = Assert.IsType<string>(createdJobs[1].job.Args[2]!);
        Assert.Equal(correspondenceIds, contCorrIds);
        Assert.Equal(attachmentIds, contAttIds);
        Assert.Equal(AllowedResourceId, contResourceId);
    }

    [Fact]
    public async Task PurgeCorrespondences_DeletesCorrespondences_EnqueuesAttachmentPurges_AndDeletesOrphanedAttachments()
    {
        // Arrange
        var handler = CreateHandler(
            out var bgMock,
            out var dialogMock,
            out var corrRepoMock,
            out var idRepoMock,
            out var attRepoMock);

        var correspondenceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var attachmentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        idRepoMock
            .Setup(m => m.DeleteByCorrespondenceIds(correspondenceIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondenceIds.Count);
        corrRepoMock
            .Setup(m => m.HardDeleteCorrespondencesByIds(correspondenceIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondenceIds.Count);

        var attachments = attachmentIds.Select(id => new AttachmentEntity
        {
            Id = id,
            ResourceId = AllowedResourceId,
            SendersReference = "ref",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            AttachmentSize = 1,
            StorageProvider = null
        }).ToList();

        attRepoMock
            .Setup(m => m.GetAttachmentsByIds(attachmentIds, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        var createdJobs = new List<(Job job, IState state)>();
        bgMock
            .Setup(m => m.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, state) => createdJobs.Add((job, state)))
            .Returns("job-storage-purge");

        attRepoMock
            .Setup(m => m.HardDeleteOrphanedAttachments(attachmentIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments.Count);

        // Act
        await handler.PurgeCorrespondences(correspondenceIds, attachmentIds, AllowedResourceId, CancellationToken.None);

        // Assert
        idRepoMock.Verify(m => m.DeleteByCorrespondenceIds(correspondenceIds, It.IsAny<CancellationToken>()), Times.Once);
        corrRepoMock.Verify(m => m.HardDeleteCorrespondencesByIds(correspondenceIds, It.IsAny<CancellationToken>()), Times.Once);
        attRepoMock.Verify(m => m.GetAttachmentsByIds(attachmentIds, true, It.IsAny<CancellationToken>()), Times.Once);
        attRepoMock.Verify(m => m.HardDeleteOrphanedAttachments(attachmentIds, It.IsAny<CancellationToken>()), Times.Once);

        var purgedAttachmentIds = createdJobs
            .Where(j => j.job.Method.Name == "PurgeAttachment")
            .Select(j => Assert.IsType<Guid>(j.job.Args[0]!))
            .ToList();
        Assert.Equal(attachmentIds.OrderBy(x => x), purgedAttachmentIds.OrderBy(x => x));
    }
}