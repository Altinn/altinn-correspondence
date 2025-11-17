using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
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
}


