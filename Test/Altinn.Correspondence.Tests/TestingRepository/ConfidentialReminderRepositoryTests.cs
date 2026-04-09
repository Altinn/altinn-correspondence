using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Persistence.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Altinn.Correspondence.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Tests.TestingRepository;

public class ConfidentialReminderRepositoryTests : IClassFixture<PostgresTestcontainerFixture>
{
    private readonly PostgresTestcontainerFixture _fixture;

    public ConfidentialReminderRepositoryTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<CorrespondenceEntity> SeedCorrespondenceAsync(TestApplicationDbContext context)
    {
        var correspondence = new CorrespondenceEntityBuilder().Build();
        context.Correspondences.Add(correspondence);
        await context.SaveChangesAsync();
        return correspondence;
    }

    private static ConfidentialReminderEntity CreateReminder(Guid correspondenceId, string recipient, Guid? dialogId = null)
    {
        return new ConfidentialReminderEntity
        {
            Id = Guid.NewGuid(),
            CorrespondenceId = correspondenceId,
            Recipient = recipient,
            DialogId = dialogId ?? Guid.NewGuid()
        };
    }

    [Fact]
    public async Task AddConfidentialReminder_NewReminder_PersistsAndReturnsId()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);
        var correspondence = await SeedCorrespondenceAsync(context);
        var reminder = CreateReminder(correspondence.Id, "urn:altinn:organization:identifier-no:991825827");

        // Act
        var returnedId = await repo.AddConfidentialReminder(reminder, CancellationToken.None);

        // Assert
        Assert.Equal(reminder.Id, returnedId);
        var saved = await context.ConfidentialReminders.FirstOrDefaultAsync(r => r.Id == reminder.Id);
        Assert.NotNull(saved);
        Assert.Equal(correspondence.Id, saved.CorrespondenceId);
        Assert.Equal(reminder.DialogId, saved.DialogId);
    }

    [Fact]
    public async Task AddConfidentialReminder_DuplicateCorrespondenceId_ReturnsExistingIdAndDoesNotInsertDuplicate()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);
        var correspondence = await SeedCorrespondenceAsync(context);
        var first = CreateReminder(correspondence.Id, "urn:altinn:organization:identifier-no:991825827");
        await context.ConfidentialReminders.AddAsync(first);
        await context.SaveChangesAsync();

        var duplicate = CreateReminder(correspondence.Id, "urn:altinn:organization:identifier-no:991825827");

        // Act
        var returnedId = await repo.AddConfidentialReminder(duplicate, CancellationToken.None);

        // Assert
        Assert.Equal(first.Id, returnedId);
        var count = await context.ConfidentialReminders.CountAsync(r => r.CorrespondenceId == correspondence.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RemoveConfidentialReminderByCorrespondenceId_ExistingReminder_RemovesIt()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);
        var correspondence = await SeedCorrespondenceAsync(context);
        var reminder = CreateReminder(correspondence.Id, "urn:altinn:organization:identifier-no:991825827");
        await context.ConfidentialReminders.AddAsync(reminder);
        await context.SaveChangesAsync();

        // Act
        await repo.RemoveConfidentialReminderByCorrespondenceId(correspondence.Id, CancellationToken.None);

        // Assert
        var exists = await context.ConfidentialReminders.AnyAsync(r => r.CorrespondenceId == correspondence.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task NumberOfRemindersForRecipient_CountsOnlyRemindersForGivenRecipient()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);
        var recipient = $"urn:altinn:organization:identifier-no:192837465";

        var c1 = await SeedCorrespondenceAsync(context);
        var c2 = await SeedCorrespondenceAsync(context);
        var c3 = await SeedCorrespondenceAsync(context);

        await context.ConfidentialReminders.AddRangeAsync(
            CreateReminder(c1.Id, recipient),
            CreateReminder(c2.Id, recipient),
            CreateReminder(c3.Id, "urn:altinn:organization:identifier-no:111222333"));
        await context.SaveChangesAsync();

        // Act
        var count = await repo.NumberOfRemindersForRecipient(recipient, CancellationToken.None);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task NumberOfRemindersForRecipient_WhenNoneExist_ReturnsZero()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);

        // Act
        var count = await repo.NumberOfRemindersForRecipient("urn:altinn:organization:identifier-no:999888777", CancellationToken.None);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CorrespondenceHasReminder_WhenReminderExists_ReturnsTrue()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);
        var correspondence = await SeedCorrespondenceAsync(context);
        var reminder = CreateReminder(correspondence.Id, "urn:altinn:organization:identifier-no:991825827");
        await context.ConfidentialReminders.AddAsync(reminder);
        await context.SaveChangesAsync();

        // Act
        var result = await repo.CorrespondenceHasReminder(correspondence.Id, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CorrespondenceHasReminder_WhenNoReminderExists_ReturnsFalse()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);

        // Act
        var result = await repo.CorrespondenceHasReminder(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetDialogIdOfReminderForRecipient_WhenReminderExists_ReturnsDialogId()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);
        var correspondence = await SeedCorrespondenceAsync(context);
        var dialogId = Guid.NewGuid();
        var recipient = $"urn:altinn:organization:identifier-no:123123123";
        var reminder = CreateReminder(correspondence.Id, recipient, dialogId);
        await context.ConfidentialReminders.AddAsync(reminder);
        await context.SaveChangesAsync();

        // Act
        var result = await repo.GetDialogIdOfReminderForRecipient(recipient, CancellationToken.None);

        // Assert
        Assert.Equal(dialogId, result);
    }

    [Fact]
    public async Task GetDialogIdOfReminderForRecipient_WhenNoReminderExists_ReturnsNull()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new ConfidentialReminderRepository(context);

        // Act
        var result = await repo.GetDialogIdOfReminderForRecipient(
            $"urn:altinn:organization:identifier-no:nonexistent-{Guid.NewGuid():N}",
            CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
