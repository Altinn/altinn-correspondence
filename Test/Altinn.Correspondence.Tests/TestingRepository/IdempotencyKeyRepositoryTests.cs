using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

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

    [Fact]
    public async Task DeleteByCorrespondenceIds_ExceedsSafetyMargin_ThrowsArgumentException()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new IdempotencyKeyRepository(context);
        var uniqueResourceId = $"safety-margin-test-exceed-{Guid.NewGuid()}";

        // Create 1001 correspondences with idempotency keys (one over the safety margin of 1000)
        var correspondences = Enumerable.Range(0, 1001)
            .Select(_ => new CorrespondenceEntityBuilder()
                .WithResourceId(uniqueResourceId)
                .Build())
            .ToList();
        context.Correspondences.AddRange(correspondences);
        await context.SaveChangesAsync();

        var idempotencyKeys = correspondences
            .Select(c => new IdempotencyKeyEntity
            {
                Id = Guid.NewGuid(),
                CorrespondenceId = c.Id,
                IdempotencyType = IdempotencyType.Correspondence
            })
            .ToList();
        context.IdempotencyKeys.AddRange(idempotencyKeys);
        await context.SaveChangesAsync();

        var correspondenceIdsToDelete = correspondences.Select(c => c.Id).ToList();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.DeleteByCorrespondenceIds(correspondenceIdsToDelete, CancellationToken.None));
        
        Assert.Contains("1001", exception.Message);
        Assert.Contains("Too many idempotency keys to delete", exception.Message);
        
        // Verify no idempotency keys were deleted by counting only our test data
        var remainingCount = await context.IdempotencyKeys
            .Where(k => k.CorrespondenceId != null && 
                        correspondences.Select(c => c.Id).Contains(k.CorrespondenceId.Value))
            .CountAsync();
        Assert.Equal(1001, remainingCount);
    }

    [Fact]
    public async Task DeleteByCorrespondenceIds_ExactlyAtSafetyMargin_DeletesSuccessfully()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new IdempotencyKeyRepository(context);
        var uniqueResourceId = $"safety-margin-test-exact-{Guid.NewGuid()}";

        // Create exactly 1000 correspondences with idempotency keys (at the safety margin limit)
        var correspondences = Enumerable.Range(0, 1000)
            .Select(_ => new CorrespondenceEntityBuilder()
                .WithResourceId(uniqueResourceId)
                .Build())
            .ToList();
        context.Correspondences.AddRange(correspondences);
        await context.SaveChangesAsync();

        var idempotencyKeys = correspondences
            .Select(c => new IdempotencyKeyEntity
            {
                Id = Guid.NewGuid(),
                CorrespondenceId = c.Id,
                IdempotencyType = IdempotencyType.Correspondence
            })
            .ToList();
        context.IdempotencyKeys.AddRange(idempotencyKeys);
        await context.SaveChangesAsync();

        var correspondenceIdsToDelete = correspondences.Select(c => c.Id).ToList();

        // Act
        var deleted = await repo.DeleteByCorrespondenceIds(correspondenceIdsToDelete, CancellationToken.None);

        // Assert
        Assert.Equal(1000, deleted);
        // Verify all our test idempotency keys were deleted
        var remainingCount = await context.IdempotencyKeys
            .Where(k => k.CorrespondenceId != null && 
                        correspondences.Select(c => c.Id).Contains(k.CorrespondenceId.Value))
            .CountAsync();
        Assert.Equal(0, remainingCount);
    }
}


