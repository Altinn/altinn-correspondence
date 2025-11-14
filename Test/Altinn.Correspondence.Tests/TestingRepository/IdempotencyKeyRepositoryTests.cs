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
        var repo = new IdempotencyKeyRepository(context);

        var c1 = new CorrespondenceEntityBuilder().Build();
        var c2 = new CorrespondenceEntityBuilder().Build();
        var c3 = new CorrespondenceEntityBuilder().Build();
        context.Correspondences.AddRange(c1, c2, c3);

        var k1 = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = c1.Id, IdempotencyType = IdempotencyType.Correspondence };
        var k2 = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = c2.Id, IdempotencyType = IdempotencyType.Correspondence };
        var k3 = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = c3.Id, IdempotencyType = IdempotencyType.Correspondence };
        var k4 = new IdempotencyKeyEntity { Id = Guid.NewGuid(), CorrespondenceId = null, AttachmentId = null, IdempotencyType = IdempotencyType.DialogportenActivity };
        context.IdempotencyKeys.AddRange(k1, k2, k3, k4);
        await context.SaveChangesAsync();

        // Act
        var deletedCount = await repo.DeleteByCorrespondenceIds([c1.Id, c3.Id], CancellationToken.None);

        // Assert
        Assert.Equal(2, deletedCount);
        Assert.Null(await context.IdempotencyKeys.FindAsync(k1.Id));
        Assert.NotNull(await context.IdempotencyKeys.FindAsync(k2.Id));
        Assert.Null(await context.IdempotencyKeys.FindAsync(k3.Id));
        Assert.NotNull(await context.IdempotencyKeys.FindAsync(k4.Id));
    }
}


