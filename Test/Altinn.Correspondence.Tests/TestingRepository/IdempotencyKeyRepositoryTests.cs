using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;

namespace Altinn.Correspondence.Tests.TestingRepository;

public class IdempotencyKeyRepositoryTests : IClassFixture<PostgresTestcontainerFixture>
{
    private readonly PostgresTestcontainerFixture _fixture;

    public IdempotencyKeyRepositoryTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteByCorrespondenceIds_DeletesOnlyMatchingCorrespondenceKeys()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var idempotencyKeyRepository = new IdempotencyKeyRepository(context);

        var correspondenceA = new CorrespondenceEntityBuilder().Build();
        var correspondenceB = new CorrespondenceEntityBuilder().Build();
        var correspondenceC = new CorrespondenceEntityBuilder().Build();
        context.Correspondences.AddRange(correspondenceA, correspondenceB, correspondenceC);

        var idempotencyKeyA = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = correspondenceA.Id, IdempotencyType = IdempotencyType.Correspondence };
        var idempotencyKeyB = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = correspondenceB.Id, IdempotencyType = IdempotencyType.Correspondence };
        var idempotencyKeyC = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = correspondenceC.Id, IdempotencyType = IdempotencyType.Correspondence };
        var idempotencyKeyD = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = null, AttachmentId = null, IdempotencyType = IdempotencyType.DialogportenActivity };
        context.IdempotencyKeys.AddRange(idempotencyKeyA, idempotencyKeyB, idempotencyKeyC, idempotencyKeyD);
        await context.SaveChangesAsync();

        // Act
        var deletedCount = await idempotencyKeyRepository.DeleteByCorrespondenceIds([correspondenceA.Id, correspondenceC.Id], CancellationToken.None);

        // Assert
        Assert.Equal(2, deletedCount);
        Assert.Null(await context.IdempotencyKeys.FindAsync(idempotencyKeyA.Id));
        Assert.NotNull(await context.IdempotencyKeys.FindAsync(idempotencyKeyB.Id));
        Assert.Null(await context.IdempotencyKeys.FindAsync(idempotencyKeyC.Id));
        Assert.NotNull(await context.IdempotencyKeys.FindAsync(idempotencyKeyD.Id));
    }
}


