using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Altinn.Correspondence.Tests.Factories;

namespace Altinn.Correspondence.Tests.TestingRepository
{
    public class CorrespondenceRepositoryTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public CorrespondenceRepositoryTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CanAddAndRetrieveCorrespondence()
        {
            // Arrange
            await using var context = _fixture.CreateDbContext();
            var correspondence = new CorrespondenceEntityBuilder()
                .Build();

            // Act
            context.Correspondences.Add(correspondence);
            await context.SaveChangesAsync();

            // Assert
            var savedCorrespondence = await context.Correspondences
                .FirstOrDefaultAsync(c => c.Id == correspondence.Id);

            Assert.NotNull(savedCorrespondence);
            Assert.Equal(correspondence.Created, savedCorrespondence.Created);
        }

        [Fact]
        public async Task LegacyCorrespondenceSearch_CorrespondenceAddedForParty_GetCorrespondencesForPartyReturnsIt()
        {
            // Arrange
            await using var context = _fixture.CreateDbContext();
            var correspondenceRepository = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTimeOffset(new DateTime(2001, 1, 1, 0, 0, 0), TimeSpan.Zero);
            var from = baseTime.AddDays(-1);
            var to = baseTime.AddDays(1);
            var recipient = "0192:987654321";
            var resource = "LegacyCorrespondenceSearch_CorrespondencesAddedForParty_GetCorrespondencesForPartyReturnsSome";
            var entity = new CorrespondenceEntityBuilder()
                .WithRecipient(recipient)
                .WithResourceId(resource)
                .WithRequestedPublishTime(baseTime)
                .WithStatus(CorrespondenceStatus.Initialized, baseTime)
                .WithStatus(CorrespondenceStatus.ReadyForPublish, baseTime.AddMinutes(1))
                .WithStatus(CorrespondenceStatus.Published, baseTime.AddMinutes(2))
                .Build();
            var addedCorrespondence = await correspondenceRepository.CreateCorrespondence(entity, CancellationToken.None);

            // Act
            var correspondences = await correspondenceRepository.GetCorrespondencesForParties(1000, from, to, null, [recipient], true, false, "", CancellationToken.None);

            // Assert
            Assert.NotNull(correspondences);
            Assert.NotEmpty(correspondences);
            Assert.Equal(1, correspondences?.Count);
            Assert.Equal(addedCorrespondence.Id, correspondences?.FirstOrDefault()?.Id);
        }

        [Fact]
        public async Task GetCorrespondencesWindowAfter_TieBreakerOnEqualCreated_UsesIdAscending()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTime(2000, 1, 1, 0, 0, 0);

            var idA = new Guid("00000000-0000-0000-0000-000000000001");
            var idB = new Guid("00000000-0000-0000-0000-000000000002");
            var items = new List<CorrespondenceEntity>
            {
                new CorrespondenceEntityBuilder()
                    .WithId(idA)
                    .WithCreated(baseTime)
                    .WithRequestedPublishTime(baseTime)
                    .WithExternalReference(ReferenceType.DialogportenDialogId, "dA")
                    .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime, Guid.NewGuid())
                    .Build(),
                new CorrespondenceEntityBuilder()
                    .WithId(idB)
                    .WithCreated(baseTime)
                    .WithRequestedPublishTime(baseTime)
                    .WithExternalReference(ReferenceType.DialogportenDialogId, "dB")
                    .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime, Guid.NewGuid())
                    .Build()
            };
            context.Correspondences.AddRange(items);
            await context.SaveChangesAsync();

            var page1 = await repo.GetCorrespondencesWindowAfter(1, null, null, true, CancellationToken.None);
            Assert.Single(page1);
            Assert.Equal(idA, page1[0].Id);

            var page2 = await repo.GetCorrespondencesWindowAfter(1, page1[0].Created, page1[0].Id, true, CancellationToken.None);
            Assert.Single(page2);
            Assert.Equal(idB, page2[0].Id);
        }

        [Fact]
        public async Task GetCorrespondencesWindowAfter_ReturnsInOrderAndPages()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTime(2000, 1, 2, 0, 0, 0);

            var items = new List<CorrespondenceEntity>
            {
                new CorrespondenceEntityBuilder()
                    .WithCreated(baseTime)
                    .WithRequestedPublishTime(baseTime)
                    .WithExternalReference(ReferenceType.DialogportenDialogId, "dA")
                    .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime, Guid.NewGuid())
                    .Build(),
                new CorrespondenceEntityBuilder()
                    .WithCreated(baseTime.AddMinutes(1))
                    .WithRequestedPublishTime(baseTime.AddMinutes(1))
                    .WithExternalReference(ReferenceType.DialogportenDialogId, "dB")
                    .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime.AddMinutes(1), Guid.NewGuid())
                    .Build(),
                new CorrespondenceEntityBuilder()
                    .WithCreated(baseTime.AddMinutes(2))
                    .WithRequestedPublishTime(baseTime.AddMinutes(2))
                    .WithExternalReference(ReferenceType.DialogportenDialogId, "dC")
                    .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime.AddMinutes(2), Guid.NewGuid())
                    .Build()
            };
            context.Correspondences.AddRange(items);
            await context.SaveChangesAsync();

            var page1 = await repo.GetCorrespondencesWindowAfter(2, null, null, true, CancellationToken.None);
            Assert.Equal(2, page1.Count);
            Assert.True(page1[0].Created <= page1[1].Created);

            var last = page1.Last();
            var page2 = await repo.GetCorrespondencesWindowAfter(2, last.Created, last.Id, true, CancellationToken.None);
            Assert.True(page2[0].Id == items[2].Id);
            Assert.True(page2[0].Created >= last.Created);
        }

        [Fact]
        public async Task GetCorrespondencesByIdsWithReferenceAndCurrentStatus_FiltersByLatestPurgedAndReference()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var baseTime = new DateTime(2002, 1, 1, 0, 0, 0);

            var shouldReturn = new CorrespondenceEntityBuilder()
                .WithCreated(baseTime)
                .WithRequestedPublishTime(baseTime)
                .WithExternalReference(ReferenceType.DialogportenDialogId, "dA")
                .WithStatus(CorrespondenceStatus.Published, baseTime.AddMinutes(1), Guid.NewGuid())
                .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime.AddMinutes(2), Guid.NewGuid())
                .Build();

            var notPurgedLatest = new CorrespondenceEntityBuilder()
                .WithCreated(baseTime)
                .WithRequestedPublishTime(baseTime)
                .WithExternalReference(ReferenceType.DialogportenDialogId, "dB")
                .WithStatus(CorrespondenceStatus.PurgedByRecipient, baseTime.AddMinutes(1), Guid.NewGuid())
                .WithStatus(CorrespondenceStatus.Archived, baseTime.AddMinutes(2), Guid.NewGuid())
                .Build();

            var noDialogRef = new CorrespondenceEntityBuilder()
                .WithCreated(baseTime)
                .WithRequestedPublishTime(baseTime)
                .WithStatus(CorrespondenceStatus.PurgedByAltinn, baseTime.AddMinutes(3), Guid.NewGuid())
                .Build();

            context.Correspondences.AddRange(shouldReturn, notPurgedLatest, noDialogRef);
            await context.SaveChangesAsync();

            var ids = new List<Guid> { shouldReturn.Id, notPurgedLatest.Id, noDialogRef.Id };
            var statuses = new List<CorrespondenceStatus> { CorrespondenceStatus.PurgedByAltinn, CorrespondenceStatus.PurgedByRecipient };
            var result = await repo.GetCorrespondencesByIdsWithExternalReferenceAndCurrentStatus(ids, ReferenceType.DialogportenDialogId, statuses, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(shouldReturn.Id, result[0].Id);
        }

        [Fact]
        public async Task GetCorrespondencesByIdsWithReferenceAndCurrentStatus_UsesLatestByStatusChangedThenStatusId()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var idA = new Guid("00000000-0000-0000-0000-000000000001");
            var idB = new Guid("00000000-0000-0000-0000-000000000002");
            var idC = new Guid("00000000-0000-0000-0000-000000000003");
            var t = new DateTime(2003, 2, 2, 0, 0, 0);

            var entity = new CorrespondenceEntityBuilder()
                .WithCreated(t)
                .WithRequestedPublishTime(t)
                .WithExternalReference(ReferenceType.DialogportenDialogId, "dD")
                .WithStatus(CorrespondenceStatus.Archived, t.AddMinutes(1), idA)
                .WithStatus(CorrespondenceStatus.PurgedByRecipient, t.AddMinutes(2), idB)
                .WithStatus(CorrespondenceStatus.Initialized, t, idC)
                .Build();

            context.Correspondences.Add(entity);
            await context.SaveChangesAsync();

            var result = await repo.GetCorrespondencesByIdsWithExternalReferenceAndCurrentStatus([entity.Id], ReferenceType.DialogportenDialogId, [CorrespondenceStatus.PurgedByRecipient], CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(entity.Id, result[0].Id);
        }

        [Fact]
        public async Task GetCorrespondencesForParties_ReturnsWhenLatestIsAttachmentsDownloaded()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var recipient = "0192:111111111";
            var from = new DateTimeOffset(new DateTime(2007, 1, 1, 0, 0, 0), TimeSpan.Zero);
            var to = from.AddDays(1);
            var baseTime = from.AddHours(1);

            var c = new CorrespondenceEntityBuilder()
                .WithRecipient(recipient)
                .WithRequestedPublishTime(baseTime)
                .WithStatus(CorrespondenceStatus.Published, baseTime.AddMinutes(1))
                .WithStatus(CorrespondenceStatus.AttachmentsDownloaded, baseTime.AddMinutes(2))
                .Build();

            context.Correspondences.Add(c);
            await context.SaveChangesAsync();

            var result = await repo.GetCorrespondencesForParties(
                limit: 10,
                from: from,
                to: to,
                status: null,
                recipientIds: [recipient],
                includeActive: true,
                includeArchived: true,
                searchString: string.Empty,
                cancellationToken: CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(c.Id, result[0].Id);
        }

        [Fact]
        public async Task GetCorrespondencesForParties_AttachmentsDownloadedExistsButSincePurged_ReturnsNothingOnIncludeOnlyActive()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var recipient = "0192:222222222";
            var from = new DateTimeOffset(new DateTime(2008, 1, 1, 0, 0, 0), TimeSpan.Zero);
            var to = from.AddDays(1);
            var baseTime = from.AddHours(1);

            var c = new CorrespondenceEntityBuilder()
                .WithRecipient(recipient)
                .WithRequestedPublishTime(baseTime)
                .WithStatus(CorrespondenceStatus.AttachmentsDownloaded, baseTime.AddMinutes(1))
                .WithStatus(CorrespondenceStatus.PurgedByRecipient, baseTime.AddMinutes(2))
                .Build();

            context.Correspondences.Add(c);
            await context.SaveChangesAsync();

            var result = await repo.GetCorrespondencesForParties(
                limit: 10,
                from: from,
                to: to,
                status: null,
                recipientIds: [recipient],
                includeActive: true,
                includeArchived: false,
                searchString: string.Empty,
                cancellationToken: CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task HardDeleteCorrespondencesByIds_DeletesOnlySpecifiedIds()
        {
            // Arrange
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var correspondenceA = new CorrespondenceEntityBuilder().Build();
            var correspondenceB = new CorrespondenceEntityBuilder().Build();
            var correspondenceC = new CorrespondenceEntityBuilder().Build();
            context.Correspondences.AddRange(correspondenceA, correspondenceB, correspondenceC);
            await context.SaveChangesAsync();

            // Act
            var deleted = await repo.HardDeleteCorrespondencesByIds([correspondenceA.Id, correspondenceC.Id], CancellationToken.None);

            // Assert
            Assert.Equal(2, deleted);
            Assert.Null(await context.Correspondences.FindAsync(correspondenceA.Id));
            Assert.NotNull(await context.Correspondences.FindAsync(correspondenceB.Id));
            Assert.Null(await context.Correspondences.FindAsync(correspondenceC.Id));
        }

        [Fact]
        public async Task HardDeleteCorrespondencesByIds_ExceedsSafetyMargin_ThrowsArgumentException()
        {
            // Arrange
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var uniqueResourceId = $"safety-margin-test-exceed-{Guid.NewGuid()}";

            // Create 1001 correspondences (one over the safety margin of 1000)
            var correspondences = Enumerable.Range(0, 1001)
                .Select(_ => new CorrespondenceEntityBuilder()
                    .WithResourceId(uniqueResourceId)
                    .Build())
                .ToList();
            context.Correspondences.AddRange(correspondences);
            await context.SaveChangesAsync();

            var idsToDelete = correspondences.Select(c => c.Id).ToList();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => repo.HardDeleteCorrespondencesByIds(idsToDelete, CancellationToken.None));
            
            Assert.Contains("1001", exception.Message);
            Assert.Contains("Too many correspondences to delete", exception.Message);
            
            // Verify no correspondences were deleted by counting only our test data
            var remainingCount = await context.Correspondences
                .Where(c => c.ResourceId == uniqueResourceId)
                .CountAsync();
            Assert.Equal(1001, remainingCount);
        }

        [Fact]
        public async Task HardDeleteCorrespondencesByIds_ExactlyAtSafetyMargin_DeletesSuccessfully()
        {
            // Arrange
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var uniqueResourceId = $"safety-margin-test-exact-{Guid.NewGuid()}";

            // Create 1000 correspondences (at the safety margin limit)
            var correspondences = Enumerable.Range(0, 1000)
                .Select(_ => new CorrespondenceEntityBuilder()
                    .WithResourceId(uniqueResourceId)
                    .Build())
                .ToList();
            context.Correspondences.AddRange(correspondences);
            await context.SaveChangesAsync();

            var idsToDelete = correspondences.Select(c => c.Id).ToList();

            // Act
            var deleted = await repo.HardDeleteCorrespondencesByIds(idsToDelete, CancellationToken.None);

            // Assert
            Assert.Equal(1000, deleted);
            // Verify all our test correspondences were deleted
            var remainingCount = await context.Correspondences
                .Where(c => c.ResourceId == uniqueResourceId)
                .CountAsync();
            Assert.Equal(0, remainingCount);
        }
    }
}
