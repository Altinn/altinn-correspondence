using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Helpers;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class PurgeCorrespondenceHelperTests
{
    private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock = new();
    private readonly Mock<IAttachmentStatusRepository> _attachmentStatusRepositoryMock = new();
    private readonly Mock<ICorrespondenceStatusRepository> _correspondenceStatusRepositoryMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly Mock<IDialogportenService> _dialogportenServiceMock = new();
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock = new();
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock = new();
    private readonly PurgeCorrespondenceHelper _helper;

    public PurgeCorrespondenceHelperTests()
    {
        _backgroundJobClientMock
            .Setup(x => x.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
            .Returns(() => Guid.NewGuid().ToString());
        _idempotencyKeyRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity?)null);
        _idempotencyKeyRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKeyEntity entity, CancellationToken _) => entity);

        _attachmentRepositoryMock
            .Setup(x => x.GetAttachmentsByCorrespondence(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentEntity>());

        _helper = new PurgeCorrespondenceHelper(
            _attachmentRepositoryMock.Object,
            _attachmentStatusRepositoryMock.Object,
            _correspondenceStatusRepositoryMock.Object,
            _backgroundJobClientMock.Object,
            _dialogportenServiceMock.Object,
            _correspondenceRepositoryMock.Object,
            _idempotencyKeyRepositoryMock.Object,
            NullLogger<PurgeCorrespondenceHelper>.Instance);
    }

    private static CorrespondenceEntity CreateCorrespondence(Guid correspondenceId)
    {
        return new CorrespondenceEntity
        {
            Id = correspondenceId,
            Sender = "urn:altinn:organization:identifier-no:313721779",
            Recipient = "urn:altinn:organization:identifier-no:310244007",
            ResourceId = "resource-123",
            SendersReference = "ref-123",
            RequestedPublishTime = DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow,
            ExternalReferences = new List<ExternalReferenceEntity>(),
            Statuses = new List<CorrespondenceStatusEntity>
            {
                new()
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString()
                }
            }
        };
    }

    [Fact]
    public async Task PurgeCorrespondence_Skips_WhenPurgeIdempotencyKeyExists()
    {
        var correspondenceId = Guid.NewGuid();
        var purgeIdempotencyId = correspondenceId.CreateVersion5("PurgeCorrespondence");
        var correspondence = CreateCorrespondence(correspondenceId);
        var partyUuid = Guid.NewGuid();

        _idempotencyKeyRepositoryMock
            .Setup(x => x.GetByIdAsync(purgeIdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyKeyEntity { Id = purgeIdempotencyId });

        var result = await _helper.PurgeCorrespondence(
            correspondence,
            isSender: true,
            partyUuid,
            partyId: 1,
            DateTimeOffset.UtcNow,
            CancellationToken.None,
            partyUrn: null);

        Assert.Equal(correspondenceId, result);
        _correspondenceStatusRepositoryMock.Verify(
            x => x.AddCorrespondenceStatus(It.IsAny<CorrespondenceStatusEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PurgeCorrespondence_Purges_WhenIdempotencyKeyDoesNotExist()
    {
        var correspondenceId = Guid.NewGuid();
        var correspondence = CreateCorrespondence(correspondenceId);
        var partyUuid = Guid.NewGuid();

        var result = await _helper.PurgeCorrespondence(
            correspondence,
            isSender: true,
            partyUuid,
            partyId: 1,
            DateTimeOffset.UtcNow,
            CancellationToken.None,
            partyUrn: null);

        Assert.Equal(correspondenceId, result);
        _correspondenceStatusRepositoryMock.Verify(
            x => x.AddCorrespondenceStatus(
                It.Is<CorrespondenceStatusEntity>(s =>
                    s.CorrespondenceId == correspondenceId &&
                    s.Status == CorrespondenceStatus.PurgedByAltinn),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _idempotencyKeyRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<IdempotencyKeyEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
