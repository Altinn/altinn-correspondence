using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class PurgeCorrespondenceHelperTests
{
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly PurgeCorrespondenceHelper _helper;

    public PurgeCorrespondenceHelperTests()
    {
        var attachmentRepository = new Mock<IAttachmentRepository>();
        attachmentRepository
            .Setup(x => x.GetAttachmentsByCorrespondence(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentEntity>());
        _backgroundJobClient
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns<Job, IState>((job, _) => job.Method.Name);

        _helper = new PurgeCorrespondenceHelper(
            attachmentRepository.Object,
            Mock.Of<IAttachmentStatusRepository>(),
            Mock.Of<ICorrespondenceStatusRepository>(),
            _backgroundJobClient.Object,
            Mock.Of<IDialogportenService>(),
            Mock.Of<ICorrespondenceRepository>(),
            Mock.Of<IIdempotencyKeyRepository>(),
            NullLogger<PurgeCorrespondenceHelper>.Instance);
    }

    [Fact]
    public async Task PurgeCorrespondence_ShouldSoftDeleteDialog_WhenCorrespondenceIsNotTransmission()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithExternalReference(ReferenceType.DialogportenDialogId, "019abaa7-6dfd-7b82-9232-a34ba1e87fbd")
            .WithStatus(CorrespondenceStatus.Initialized)
            .Build();

        // Act
        await _helper.PurgeCorrespondence(correspondence, true, Guid.NewGuid(), 1, DateTimeOffset.UtcNow, CancellationToken.None, null);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Method.Name == nameof(IDialogportenService.TrySoftDeleteDialog)),
            It.IsAny<IState>()),
            Times.Once);
    }

    [Fact]
    public async Task PurgeCorrespondence_ShouldNotChangeDialog_WhenCorrespondenceIsTransmission()
    {
        // Arrange
        var correspondence = new CorrespondenceEntityBuilder()
            .WithExternalReference(ReferenceType.DialogportenDialogId, "019abaa7-6dfd-7b82-9232-a34ba1e87fbd")
            .WithExternalReference(ReferenceType.DialogportenTransmissionType, "Information")
            .WithStatus(CorrespondenceStatus.Initialized)
            .Build();

        // Act
        await _helper.PurgeCorrespondence(correspondence, true, Guid.NewGuid(), 1, DateTimeOffset.UtcNow, CancellationToken.None, null);

        // Assert
        _backgroundJobClient.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IDialogportenService) || job.Type == typeof(PurgeCorrespondenceHelper)),
            It.IsAny<IState>()),
            Times.Never);
    }
}
