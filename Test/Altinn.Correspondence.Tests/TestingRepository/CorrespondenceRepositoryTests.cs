using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
            var correspondence = new CorrespondenceEntity
            {
                Created = DateTime.UtcNow,
                Recipient = "0192:987654321",
                RequestedPublishTime = DateTime.UtcNow,
                ResourceId = "1",
                Sender = "0192:123456789",
                SendersReference = "1",
                Statuses = new List<CorrespondenceStatusEntity>(),
                PropertyList = new Dictionary<string, string>()
            };

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
            var from = DateTimeOffset.UtcNow.AddDays(-1);
            var to = DateTimeOffset.UtcNow.AddDays(1);
            var recipient = "0192:987654321";
            var resource = "LegacyCorrespondenceSearch_CorrespondencesAddedForParty_GetCorrespondencesForPartyReturnsSome";
            var addedCorrespondence = await correspondenceRepository.CreateCorrespondence(new CorrespondenceEntity()
            {
                Created = DateTimeOffset.UtcNow,
                Recipient = recipient,
                RequestedPublishTime = DateTimeOffset.UtcNow,
                ResourceId = resource,
                Sender = "0192:123456789",
                SendersReference = "1",
                Statuses = new List<CorrespondenceStatusEntity>()
                {
                    new CorrespondenceStatusEntity()
                    {
                        Status = Core.Models.Enums.CorrespondenceStatus.Initialized
                    },
                    new CorrespondenceStatusEntity()
                    {
                        Status = Core.Models.Enums.CorrespondenceStatus.ReadyForPublish
                    },
                    new CorrespondenceStatusEntity(){
                        Status = Core.Models.Enums.CorrespondenceStatus.Published
                    }
                },
            }, CancellationToken.None);

            // Act
            var correspondences = await correspondenceRepository.GetCorrespondencesForParties(1000, from, to, null, [recipient], [resource], true, false, false, "", CancellationToken.None);

            // Assert
            Assert.NotNull(correspondences);
            Assert.NotEmpty(correspondences);
            Assert.Equal(1, correspondences?.Count);
            Assert.Equal(addedCorrespondence.Id, correspondences.FirstOrDefault()?.Id);
        }

        [Fact]
        public async Task GetPurgedCorrespondencesWithDialogsAfter_TieBreakerOnEqualCreated_UsesIdAscending()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var idA = new Guid("00000000-0000-0000-0000-000000000001");
            var idB = new Guid("00000000-0000-0000-0000-000000000002");
            var items = new List<CorrespondenceEntity>
            {
                new CorrespondenceEntity
                {
                    Id = idA,
                    Created = baseTime,
                    Recipient = "0192:987654321",
                    RequestedPublishTime = baseTime,
                    ResourceId = "r",
                    Sender = "0192:123456789",
                    SendersReference = "A",
                    Statuses = new List<CorrespondenceStatusEntity> { new() { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime } },
                    ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "dA" } }
                },
                new CorrespondenceEntity
                {
                    Id = idB,
                    Created = baseTime,
                    Recipient = "0192:987654321",
                    RequestedPublishTime = baseTime,
                    ResourceId = "r",
                    Sender = "0192:123456789",
                    SendersReference = "B",
                    Statuses = new List<CorrespondenceStatusEntity> { new() { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime } },
                    ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "dB" } }
                }
            };
            context.Correspondences.AddRange(items);
            await context.SaveChangesAsync();

            var page1 = await repo.GetPurgedCorrespondencesWithDialogsAfter(1, null, null, true, CancellationToken.None);
            Assert.Single(page1);
            Assert.Equal(idA, page1[0].Id);

            var page2 = await repo.GetPurgedCorrespondencesWithDialogsAfter(1, page1[0].Created, page1[0].Id, true, CancellationToken.None);
            Assert.Single(page2);
            Assert.Equal(idB, page2[0].Id);
        }

        [Fact]
        public async Task GetPurgedCorrespondencesWithDialogsAfter_ReturnsInOrderAndPages()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTimeOffset(2000, 1, 2, 0, 0, 0, TimeSpan.Zero);

            var items = new List<CorrespondenceEntity>
            {
                new CorrespondenceEntity
                {
                    Created = baseTime,
                    Recipient = "0192:987654321",
                    RequestedPublishTime = baseTime,
                    ResourceId = "r",
                    Sender = "0192:123456789",
                    SendersReference = "A",
                    Statuses = new List<CorrespondenceStatusEntity> { new() { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime } },
                    ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "dA" } }
                },
                new CorrespondenceEntity
                {
                    Created = baseTime.AddMinutes(1),
                    Recipient = "0192:987654321",
                    RequestedPublishTime = baseTime.AddMinutes(1),
                    ResourceId = "r",
                    Sender = "0192:123456789",
                    SendersReference = "B",
                    Statuses = new List<CorrespondenceStatusEntity> { new() { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime.AddMinutes(1) } },
                    ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "dB" } }
                },
                new CorrespondenceEntity
                {
                    Created = baseTime.AddMinutes(2),
                    Recipient = "0192:987654321",
                    RequestedPublishTime = baseTime.AddMinutes(2),
                    ResourceId = "r",
                    Sender = "0192:123456789",
                    SendersReference = "C",
                    Statuses = new List<CorrespondenceStatusEntity> { new() { Status = CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime.AddMinutes(2) } },
                    ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = ReferenceType.DialogportenDialogId, ReferenceValue = "dC" } }
                }
            };
            context.Correspondences.AddRange(items);
            await context.SaveChangesAsync();

            var page1 = await repo.GetPurgedCorrespondencesWithDialogsAfter(2, null, null, true, CancellationToken.None);
            Assert.Equal(2, page1.Count);
            Assert.True(page1[0].Created <= page1[1].Created);

            var last = page1.Last();
            var page2 = await repo.GetPurgedCorrespondencesWithDialogsAfter(2, last.Created, last.Id, true, CancellationToken.None);
            Assert.Single(page2);
            Assert.True(page2[0].Created >= last.Created);
        }
    }
}
