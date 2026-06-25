using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class HangfireScheduleHelperTests
{
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock = new();
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock = new();

    public HangfireScheduleHelperTests()
    {
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(() => Guid.NewGuid().ToString());
        _idempotencyKeyRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity?)null);
        _idempotencyKeyRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity entity, CancellationToken _) => entity);
    }

    private HangfireScheduleHelper CreateHelper(ApplicationDbContext? dbContext = null)
    {
        return new HangfireScheduleHelper(
            _backgroundJobClientMock.Object,
            Mock.Of<IHybridCacheWrapper>(),
            _correspondenceRepositoryMock.Object,
            _idempotencyKeyRepositoryMock.Object,
            dbContext ?? TestDbContextFactory.Create(),
            NullLogger<HangfireScheduleHelper>.Instance);
    }

    private static CorrespondenceEntity CreateSchedulableCorrespondence(Guid correspondenceId)
    {
        return new CorrespondenceEntity
        {
            Id = correspondenceId,
            Sender = "urn:altinn:organization:identifier-no:313721779",
            Recipient = "urn:altinn:organization:identifier-no:310244007",
            ResourceId = "resource-123",
            SendersReference = "ref-123",
            RequestedPublishTime = DateTimeOffset.UtcNow.AddMinutes(5),
            Created = DateTimeOffset.UtcNow.AddMinutes(-30),
            Statuses = new List<CorrespondenceStatusEntity>()
        };
    }

    [Fact]
    public async Task SchedulePublishAtPublishTime_SchedulesPublish_WhenNotDuplicate()
    {
        var correspondenceId = Guid.NewGuid();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(CreateSchedulableCorrespondence(correspondenceId));

        await CreateHelper().SchedulePublishAtPublishTime(correspondenceId, CancellationToken.None);

        _backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(j => j.Type == typeof(PublishCorrespondenceHandler)),
            It.Is<IState>(s => s is ScheduledState)),
            Times.Once);
        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SchedulePublishAtPublishTime_Skips_WhenScheduleIdempotencyKeyExists()
    {
        var correspondenceId = Guid.NewGuid();
        var scheduleIdempotencyId = correspondenceId.CreateVersion5("SchedulePublishCorrespondence");
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(CreateSchedulableCorrespondence(correspondenceId));
        _idempotencyKeyRepositoryMock
            .Setup(x => x.GetByIdAsync(scheduleIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyKeyEntity { Id = scheduleIdempotencyId });

        await CreateHelper().SchedulePublishAtPublishTime(correspondenceId, CancellationToken.None);

        _backgroundJobClientMock.Verify(
            x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Never);
        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SchedulePublishAtPublishTime_Skips_WhenUniqueViolationOnFlush()
    {
        var correspondenceId = Guid.NewGuid();
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondenceId, true, false, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(CreateSchedulableCorrespondence(correspondenceId));

        await CreateHelper(TestDbContextFactory.CreateUniqueViolationOnDeferredSave())
            .SchedulePublishAtPublishTime(correspondenceId, CancellationToken.None);

        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
