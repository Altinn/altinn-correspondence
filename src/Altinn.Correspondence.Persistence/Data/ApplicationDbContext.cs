using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<AttachmentEntity> Attachments { get; set; }
    public DbSet<AttachmentStatusEntity> AttachmentStatuses { get; set; }
    public DbSet<CorrespondenceAttachmentEntity> CorrespondenceAttachments { get; set; }
    public DbSet<CorrespondenceContentEntity> CorrespondenceContents { get; set; }
    public DbSet<CorrespondenceEntity> Correspondences { get; set; }
    public DbSet<CorrespondenceStatusEntity> CorrespondenceStatuses { get; set; }
    public DbSet<CorrespondenceForwardingEventEntity> CorrespondenceForwardingEvents { get; set; }
    public DbSet<CorrespondenceNotificationEntity> CorrespondenceNotifications { get; set; }
    public DbSet<CorrespondenceReplyOptionEntity> CorrespondenceReplyOptions { get; set; }
    public DbSet<CorrespondenceDeleteEventEntity> CorrespondenceDeleteEvents { get; set; }
    public DbSet<ExternalReferenceEntity> ExternalReferences { get; set; }
    public DbSet<NotificationTemplateEntity> NotificationTemplates { get; set; }
    public DbSet<LegacyPartyEntity> LegacyParties { get; set; }
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys { get; set; } = null!;
    public DbSet<ServiceOwnerEntity> ServiceOwners { get; set; }
    public DbSet<StorageProviderEntity> StorageProviders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("correspondence");
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }
}
