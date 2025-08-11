using Altinn.Correspondence.Core.Models.Entities;
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
        public async Task GetPurgedCorrespondencesWithDialogsAfter_ReturnsInOrderAndPages()
        {
            await using var context = _fixture.CreateDbContext();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

            var items = new List<CorrespondenceEntity>();
            for (int i = 0; i < 3; i++)
            {
                items.Add(new CorrespondenceEntity
                {
                    Created = baseTime.AddMinutes(i),
                    Recipient = "0192:987654321",
                    RequestedPublishTime = baseTime.AddMinutes(i),
                    ResourceId = "r",
                    Sender = "0192:123456789",
                    SendersReference = i.ToString(),
                    Statuses = new List<CorrespondenceStatusEntity> { new() { Status = Altinn.Correspondence.Core.Models.Enums.CorrespondenceStatus.PurgedByAltinn, StatusChanged = baseTime.AddMinutes(i) } },
                    ExternalReferences = new List<ExternalReferenceEntity> { new() { ReferenceType = Altinn.Correspondence.Core.Models.Enums.ReferenceType.DialogportenDialogId, ReferenceValue = $"d{i}" } }
                });
            }
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
