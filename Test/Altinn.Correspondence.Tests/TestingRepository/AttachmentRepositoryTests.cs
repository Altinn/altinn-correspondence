using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Altinn.Correspondence.Tests.TestingRepository;

public class AttachmentRepositoryTests : IClassFixture<PostgresTestcontainerFixture>
{
    private readonly PostgresTestcontainerFixture _fixture;

    public AttachmentRepositoryTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HardDeleteOrphanedAttachments_DeletesOnlyOrphansWithinProvidedList()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());

        var orphanA = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-a",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            AttachmentSize = 1
        };
        var linkedB = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-b",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            AttachmentSize = 1
        };
        var orphanC = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-c",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            AttachmentSize = 1
        };
        var orphanD = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-d",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            AttachmentSize = 1
        };

        var correspondence = new CorrespondenceEntityBuilder().Build();
        correspondence.Content!.Attachments.Add(new CorrespondenceAttachmentEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceContentId = correspondence.Content!.Id,
            AttachmentId = linkedB.Id,
            Attachment = linkedB,
            Created = correspondence.Created,
            ExpirationTime = correspondence.Created.AddDays(30)
        });

        context.Attachments.AddRange(orphanA, linkedB, orphanC, orphanD);
        context.Correspondences.Add(correspondence);
        await context.SaveChangesAsync();

        // Act
        var deleted = await repo.HardDeleteOrphanedAttachments([orphanA.Id, linkedB.Id, orphanC.Id], CancellationToken.None);

        // Assert
        Assert.Equal(2, deleted); // orphanA and orphanC
        Assert.Null(await context.Attachments.FindAsync(orphanA.Id));
        Assert.NotNull(await context.Attachments.FindAsync(linkedB.Id)); // linked should remain
        Assert.Null(await context.Attachments.FindAsync(orphanC.Id));
        Assert.NotNull(await context.Attachments.FindAsync(orphanD.Id)); // not in list, should remain
    }

    [Fact]
    public async Task HardDeleteOrphanedAttachments_ExceedsSafetyMargin_ThrowsArgumentException()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());
        var uniqueResourceId = $"safety-margin-test-exceed-{Guid.NewGuid()}";

        // Create 1001 orphaned attachments (one over the safety margin of 1000)
        var attachments = Enumerable.Range(0, 1001)
            .Select(i => new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                ResourceId = uniqueResourceId,
                SendersReference = $"ref-{i}",
                Sender = "0192:910753614",
                Created = DateTimeOffset.UtcNow,
                AttachmentSize = 1
            })
            .ToList();
        context.Attachments.AddRange(attachments);
        await context.SaveChangesAsync();

        var idsToDelete = attachments.Select(a => a.Id).ToList();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.HardDeleteOrphanedAttachments(idsToDelete, CancellationToken.None));
        
        Assert.Contains("1001", exception.Message);
        Assert.Contains("Too many orphaned attachments to delete", exception.Message);
        
        // Verify no attachments were deleted by counting only our test data
        var remainingCount = await context.Attachments
            .Where(a => a.ResourceId == uniqueResourceId)
            .CountAsync();
        Assert.Equal(1001, remainingCount);
    }

    [Fact]
    public async Task HardDeleteOrphanedAttachments_ExactlyAtSafetyMargin_DeletesSuccessfully()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());
        var uniqueResourceId = $"safety-margin-test-exact-{Guid.NewGuid()}";

        // Create exactly 1000 orphaned attachments (at the safety margin limit)
        var attachments = Enumerable.Range(0, 1000)
            .Select(i => new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                ResourceId = uniqueResourceId,
                SendersReference = $"ref-{i}",
                Sender = "0192:910753614",
                Created = DateTimeOffset.UtcNow,
                AttachmentSize = 1
            })
            .ToList();
        context.Attachments.AddRange(attachments);
        await context.SaveChangesAsync();

        var idsToDelete = attachments.Select(a => a.Id).ToList();

        // Act
        var deleted = await repo.HardDeleteOrphanedAttachments(idsToDelete, CancellationToken.None);

        // Assert
        Assert.Equal(1000, deleted);
        // Verify all our test attachments were deleted
        var remainingCount = await context.Attachments
            .Where(a => a.ResourceId == uniqueResourceId)
            .CountAsync();
        Assert.Equal(0, remainingCount);
    }
}


