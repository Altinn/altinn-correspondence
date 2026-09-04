using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Altinn.Correspondence.Tests.TestingRepository
{
    public class CorrespondenceRepositoryTests
    {
        [Fact]
        public async Task CanAddAndRetrieveCorrespondence()
        {
            // Arrange
            await using var context = TestDbContextFactory.Create();
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
        public async Task GetCorrespondences_AsSender_ReturnsArchivedCorrespondence()
        {
            // Arrange
            await using var context = TestDbContextFactory.Create();
            var correspondenceRepository = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTimeOffset(new DateTime(2002, 1, 1, 0, 0, 0), TimeSpan.Zero);
            var from = baseTime.AddDays(-1);
            var to = baseTime.AddDays(1);
            var senderOrgNo = "991825827";
            var resource = Guid.NewGuid().ToString();
            var entity = new CorrespondenceEntityBuilder()
                .WithResourceId(resource)
                .WithRequestedPublishTime(baseTime)
                .WithStatus(CorrespondenceStatus.Published, baseTime.AddMinutes(1))
                .WithStatus(CorrespondenceStatus.Archived, baseTime.AddMinutes(2))
                .Build();
            var addedCorrespondence = await correspondenceRepository.CreateCorrespondence(entity, CancellationToken.None);

            // Act
            var byArchivedStatus = await correspondenceRepository.GetCorrespondences(resource, 1000, from, to, CorrespondenceStatus.Archived, senderOrgNo, CorrespondencesRoleType.Sender, null, default, CancellationToken.None);
            var withoutStatus = await correspondenceRepository.GetCorrespondences(resource, 1000, from, to, null, senderOrgNo, CorrespondencesRoleType.Sender, null, default, CancellationToken.None);

            // Assert
            Assert.Contains(addedCorrespondence.Id, byArchivedStatus);
            Assert.Contains(addedCorrespondence.Id, withoutStatus);
        }

        [Theory]
        [InlineData("SEnDeROrgNuMBeR")]
        [InlineData("senderorgnumber")]
        [InlineData("senderOrgNumber")]
        public async Task GetDailySummaryData_PropertyListContainsSenderOrgNumber_PopulatesSenderOrgNumber(string senderOrgNumberKey)
        {
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var serviceOwnerId = $"so-{Guid.NewGuid():N}";
            var resourceId = $"test-resource-{Guid.NewGuid():N}";
            var messageSender = $"test-sender-{Guid.NewGuid():N}";

            // Service owner must exist; otherwise GetDailySummaryData filters the correspondence out.
            context.ServiceOwners.Add(new ServiceOwnerEntity
            {
                Id = serviceOwnerId,
                Name = "Test Service Owner",
                StorageProviders = new List<StorageProviderEntity>()
            });

            var created = new DateTime(2026, 01, 02, 00, 00, 00, DateTimeKind.Utc);
            var correspondence = new CorrespondenceEntityBuilder()
                .WithServiceOwnerId(serviceOwnerId)
                .WithCreated(created)
                .WithResourceId(resourceId)
                .WithPropertyList(new Dictionary<string, string>
                {
                    // Use different casing to verify case-insensitive fallback logic
                    [senderOrgNumberKey] = "987654321",
                    ["other"] = "value"
                })
                .Build();
            correspondence.Altinn2CorrespondenceId = null;
            correspondence.MessageSender = messageSender;
            correspondence.RecipientType = UrnConstants.OrganizationNumberAttribute;

            context.Correspondences.Add(correspondence);
            await context.SaveChangesAsync();

            var result = await repo.GetDailySummaryData(includeAltinn2: false, cancellationToken: CancellationToken.None);

            var row = Assert.Single(result, r =>
                r.ServiceOwnerId == serviceOwnerId &&
                r.ResourceId == resourceId &&
                r.MessageSender == messageSender &&
                r.Date == created.Date);
            Assert.Equal(correspondence.Id, row.CorrespondenceId);
            Assert.Equal(1, row.MessageCount);
            Assert.Equal("987654321", row.SenderOrgNumber);
            Assert.Null(row.ShipmentId);
            Assert.Null(row.ReminderShipmentId);
        }

        [Fact]
        public async Task GetDailySummaryData_ReturnsOneRowPerCorrespondence_WithNotificationShipmentIds()
        {
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var serviceOwnerId = $"so-{Guid.NewGuid():N}";
            var resourceId = $"test-resource-{Guid.NewGuid():N}";
            var mainShipmentId = Guid.NewGuid();
            var reminderShipmentId = Guid.NewGuid();
            var created = new DateTime(2026, 01, 02, 00, 00, 00, DateTimeKind.Utc);

            context.ServiceOwners.Add(new ServiceOwnerEntity
            {
                Id = serviceOwnerId,
                Name = "Test Service Owner",
                StorageProviders = new List<StorageProviderEntity>()
            });

            var correspondence1 = new CorrespondenceEntityBuilder()
                .WithServiceOwnerId(serviceOwnerId)
                .WithCreated(created)
                .WithResourceId(resourceId)
                .Build();
            correspondence1.Altinn2CorrespondenceId = null;
            correspondence1.MessageSender = "sender-a";
            correspondence1.RecipientType = UrnConstants.OrganizationNumberAttribute;
            correspondence1.Notifications =
            [
                new CorrespondenceNotificationEntity
                {
                    Id = Guid.NewGuid(),
                    CorrespondenceId = correspondence1.Id,
                    Created = created,
                    IsReminder = false,
                    ShipmentId = mainShipmentId,
                    NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                    NotificationChannel = NotificationChannel.Email,
                    RequestedSendTime = created
                },
                new CorrespondenceNotificationEntity
                {
                    Id = Guid.NewGuid(),
                    CorrespondenceId = correspondence1.Id,
                    Created = created.AddDays(1),
                    IsReminder = true,
                    ShipmentId = reminderShipmentId,
                    NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
                    NotificationChannel = NotificationChannel.Email,
                    RequestedSendTime = created.AddDays(1)
                }
            ];

            var correspondence2 = new CorrespondenceEntityBuilder()
                .WithServiceOwnerId(serviceOwnerId)
                .WithCreated(created)
                .WithResourceId(resourceId)
                .Build();
            correspondence2.Altinn2CorrespondenceId = null;
            correspondence2.MessageSender = "sender-a";
            correspondence2.RecipientType = UrnConstants.OrganizationNumberAttribute;

            context.Correspondences.AddRange(correspondence1, correspondence2);
            await context.SaveChangesAsync();

            var result = await repo.GetDailySummaryData(includeAltinn2: false, cancellationToken: CancellationToken.None);

            var rowsForResource = result
                .Where(r => r.ServiceOwnerId == serviceOwnerId && r.ResourceId == resourceId)
                .OrderBy(r => r.CorrespondenceId)
                .ToList();

            Assert.Equal(2, rowsForResource.Count);
            Assert.All(rowsForResource, r => Assert.Equal(1, r.MessageCount));

            var rowWithNotifications = Assert.Single(rowsForResource, r => r.CorrespondenceId == correspondence1.Id);
            Assert.Equal(mainShipmentId, rowWithNotifications.ShipmentId);
            Assert.Equal(reminderShipmentId, rowWithNotifications.ReminderShipmentId);

            var rowWithoutNotifications = Assert.Single(rowsForResource, r => r.CorrespondenceId == correspondence2.Id);
            Assert.Null(rowWithoutNotifications.ShipmentId);
            Assert.Null(rowWithoutNotifications.ReminderShipmentId);
        }

        [Fact]
        public async Task GetCorrespondencesWindowAfter_TieBreakerOnEqualCreated_UsesIdAscending()
        {
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            var baseTime = new DateTime(2000, 1, 1, 0, 0, 0);

            var sequentialIds = SequentialGuids(3);
            var previousId = sequentialIds[0];
            var idA = sequentialIds[1];
            var idB = sequentialIds[2];
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

            var page1 = await repo.GetCorrespondencesWindowAfter(1, baseTime, previousId, true, CancellationToken.None);
            Assert.Single(page1);
            Assert.Equal(idA, page1[0].Id);

            var page2 = await repo.GetCorrespondencesWindowAfter(1, page1[0].Created, page1[0].Id, true, CancellationToken.None);
            Assert.Single(page2);
            Assert.Equal(idB, page2[0].Id);
        }

        [Fact]
        public async Task GetCorrespondencesWindowAfter_ReturnsInOrderAndPages()
        {
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());
            // Use a far-future window so this paging test is isolated from other repository tests
            // that insert UtcNow-based correspondences into the shared PostgreSQL test database.
            var baseTime = DateTime.UtcNow.AddYears(100);

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

            var page1 = await repo.GetCorrespondencesWindowAfter(2, baseTime.AddMilliseconds(-1), null, true, CancellationToken.None);
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
            await using var context = TestDbContextFactory.Create();
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
            await using var context = TestDbContextFactory.Create();
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
        public async Task HardDeleteCorrespondencesByIds_DeletesOnlySpecifiedIds()
        {
            // Arrange
            await using var context = TestDbContextFactory.Create();
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
        public async Task HardDeleteCorrespondencesByIds_ExceedsSafetyMargin_ThrowsAndDeletesNothing()
        {
            // Arrange
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>(), maxHardDeleteBatchSize: 2);
            var uniqueResourceId = $"safety-margin-test-exceed-{Guid.NewGuid()}";

            // Three correspondences, one over the configured safety margin of two
            var correspondences = Enumerable.Range(0, 3)
                .Select(_ => new CorrespondenceEntityBuilder().WithResourceId(uniqueResourceId).Build())
                .ToList();
            context.Correspondences.AddRange(correspondences);
            await context.SaveChangesAsync();

            var idsToDelete = correspondences.Select(c => c.Id).ToList();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => repo.HardDeleteCorrespondencesByIds(idsToDelete, CancellationToken.None));
            Assert.Contains("3", exception.Message);
            Assert.Contains("Too many correspondences to delete", exception.Message);

            var remainingCount = await context.Correspondences
                .Where(c => c.ResourceId == uniqueResourceId)
                .CountAsync();
            Assert.Equal(3, remainingCount);
        }

        [Fact]
        public async Task HardDeleteCorrespondencesByIds_AtSafetyMargin_DeletesSuccessfully()
        {
            // Arrange
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>(), maxHardDeleteBatchSize: 2);
            var uniqueResourceId = $"safety-margin-test-exact-{Guid.NewGuid()}";

            // Two correspondences, exactly at the configured safety margin
            var correspondences = Enumerable.Range(0, 2)
                .Select(_ => new CorrespondenceEntityBuilder().WithResourceId(uniqueResourceId).Build())
                .ToList();
            context.Correspondences.AddRange(correspondences);
            await context.SaveChangesAsync();

            var idsToDelete = correspondences.Select(c => c.Id).ToList();

            // Act
            var deleted = await repo.HardDeleteCorrespondencesByIds(idsToDelete, CancellationToken.None);

            // Assert
            Assert.Equal(2, deleted);
            var remainingCount = await context.Correspondences
                .Where(c => c.ResourceId == uniqueResourceId)
                .CountAsync();
            Assert.Equal(0, remainingCount);
        }

        [Fact]
        public async Task GetCorrespondencesWithAltinn2IdNotMigratingAndConfirmedStatus_FiltersOutInvalidCandidates()
        {
            await using var context = TestDbContextFactory.Create();
            var repo = new CorrespondenceRepository(context, new NullLogger<ICorrespondenceRepository>());

            var baseTime = new DateTime(2010, 1, 1, 0, 0, 0);
            var altinn2IdBase = Random.Shared.Next(1_000_000, int.MaxValue - 10);

            var valid = new CorrespondenceEntityBuilder()
                .WithRequestedPublishTime(baseTime)
                .WithAltinn2CorrespondenceId(altinn2IdBase + 1)
                .WithStatus(CorrespondenceStatus.Confirmed, baseTime.AddMinutes(1))
                .Build();

            var notConfirmed = new CorrespondenceEntityBuilder()
                .WithRequestedPublishTime(baseTime.AddMinutes(2))
                .WithAltinn2CorrespondenceId(altinn2IdBase + 3)
                .WithStatus(CorrespondenceStatus.Published, baseTime.AddMinutes(2))
                .Build();

            var noAltinn2Id = new CorrespondenceEntityBuilder()
                .WithRequestedPublishTime(baseTime.AddMinutes(3))
                .WithStatus(CorrespondenceStatus.Confirmed, baseTime.AddMinutes(3))
                .Build();

            context.Correspondences.AddRange(valid, notConfirmed, noAltinn2Id);
            await context.SaveChangesAsync();

            var windowIds = new List<Guid> { valid.Id, notConfirmed.Id, noAltinn2Id.Id };

            var result = await repo.GetCorrespondencesWithAltinn2IdNotMigratingAndConfirmedStatus(windowIds, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(valid.Id, result[0].Id);
        }

        private static Guid[] SequentialGuids(int count)
        {
            // 24 hex chars of randomness per run, last 8 hex chars are the sequence
            var prefix = Guid.NewGuid().ToString("N")[..24];
            return Enumerable.Range(0, count)
                .Select(i => Guid.ParseExact(prefix + i.ToString("x8"), "N"))
                .ToArray();
        }
    }
}
