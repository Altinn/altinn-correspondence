using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
            var correspondenceRepository = new CorrespondenceRepository(context, new HttpContextAccessor());
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
    }
}
