using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Altinn.Correspondence.Tests.TestingRepository;

public class AttachmentRepositoryTests
{
    [Fact]
    public async Task HardDeleteOrphanedAttachments_DeletesOnlyOrphansWithinProvidedList()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());

        var orphanA = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-a",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            FileName = "file-a.txt",
            AttachmentSize = 1
        };
        var linkedB = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-b",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            FileName = "file-b.txt",
            AttachmentSize = 1
        };
        var orphanC = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-c",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            FileName = "file-c.txt",
            AttachmentSize = 1
        };
        var orphanD = new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref-d",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            FileName = "file-d.txt",
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
    public async Task HardDeleteOrphanedAttachments_ExceedsSafetyMargin_ThrowsAndDeletesNothing()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>(), maxHardDeleteBatchSize: 2);
        var uniqueResourceId = $"safety-margin-test-exceed-{Guid.NewGuid()}";

        // Three orphaned attachments, one over the configured safety margin of two
        var attachments = Enumerable.Range(0, 3)
            .Select(i => new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                ResourceId = uniqueResourceId,
                SendersReference = $"ref-{i}",
                Sender = "0192:910753614",
                Created = DateTimeOffset.UtcNow,
                FileName = $"file-{i}.txt",
                AttachmentSize = 1
            })
            .ToList();
        context.Attachments.AddRange(attachments);
        await context.SaveChangesAsync();

        var idsToDelete = attachments.Select(a => a.Id).ToList();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.HardDeleteOrphanedAttachments(idsToDelete, CancellationToken.None));
        Assert.Contains("3", exception.Message);
        Assert.Contains("Too many orphaned attachments to delete", exception.Message);

        var remainingCount = await context.Attachments
            .Where(a => a.ResourceId == uniqueResourceId)
            .CountAsync();
        Assert.Equal(3, remainingCount);
    }

    [Fact]
    public async Task HardDeleteOrphanedAttachments_AtSafetyMargin_DeletesSuccessfully()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>(), maxHardDeleteBatchSize: 2);
        var uniqueResourceId = $"safety-margin-test-exact-{Guid.NewGuid()}";

        // Two orphaned attachments, exactly at the configured safety margin
        var attachments = Enumerable.Range(0, 2)
            .Select(i => new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                ResourceId = uniqueResourceId,
                SendersReference = $"ref-{i}",
                Sender = "0192:910753614",
                Created = DateTimeOffset.UtcNow,
                FileName = $"file-{i}.txt",
                AttachmentSize = 1
            })
            .ToList();
        context.Attachments.AddRange(attachments);
        await context.SaveChangesAsync();

        var idsToDelete = attachments.Select(a => a.Id).ToList();

        // Act
        var deleted = await repo.HardDeleteOrphanedAttachments(idsToDelete, CancellationToken.None);

        // Assert
        Assert.Equal(2, deleted);
        var remainingCount = await context.Attachments
            .Where(a => a.ResourceId == uniqueResourceId)
            .CountAsync();
        Assert.Equal(0, remainingCount);
    }

    [Fact]
    public async Task GetTotalAttachmentSizeByCorrespondence_SumsOnlyAttachmentsOnThatCorrespondence()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());

        var correspondence = new CorrespondenceEntityBuilder().Build();
        LinkAttachment(correspondence, BuildAttachment(6_000_000));
        LinkAttachment(correspondence, BuildAttachment(4_000_001));
        var otherCorrespondence = new CorrespondenceEntityBuilder().Build();
        LinkAttachment(otherCorrespondence, BuildAttachment(50_000_000));

        context.Correspondences.AddRange(correspondence, otherCorrespondence);
        await context.SaveChangesAsync();

        // Act
        var totalSize = await repo.GetTotalAttachmentSizeByCorrespondence(correspondence.Id, CancellationToken.None);

        // Assert
        Assert.Equal(10_000_001, totalSize);
    }

    [Fact]
    public async Task GetTotalAttachmentSizeByCorrespondence_NoAttachments_ReturnsZero()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());
        var correspondence = new CorrespondenceEntityBuilder().Build();
        context.Correspondences.Add(correspondence);
        await context.SaveChangesAsync();

        // Act
        var totalSize = await repo.GetTotalAttachmentSizeByCorrespondence(correspondence.Id, CancellationToken.None);

        // Assert
        Assert.Equal(0, totalSize);
    }

    [Fact]
    public async Task GetTotalAttachmentSizeByCorrespondence_UnknownCorrespondence_ReturnsZero()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();
        var repo = new AttachmentRepository(context, new NullLogger<IAttachmentRepository>());

        // Act
        var totalSize = await repo.GetTotalAttachmentSizeByCorrespondence(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Equal(0, totalSize);
    }

    private static AttachmentEntity BuildAttachment(long attachmentSize)
    {
        return new AttachmentEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = "res-1",
            SendersReference = "ref",
            Sender = "0192:910753614",
            Created = DateTimeOffset.UtcNow,
            FileName = "file.txt",
            AttachmentSize = attachmentSize
        };
    }

    private static void LinkAttachment(CorrespondenceEntity correspondence, AttachmentEntity attachment)
    {
        correspondence.Content!.Attachments.Add(new CorrespondenceAttachmentEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceContentId = correspondence.Content!.Id,
            AttachmentId = attachment.Id,
            Attachment = attachment,
            Created = correspondence.Created,
            ExpirationTime = correspondence.Created.AddDays(30)
        });
    }
}


