using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Persistence.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Altinn.Correspondence.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
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
    public DbSet<CorrespondenceStatusFetchedEntity> CorrespondenceFetches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("correspondence");

        // Configure StorageProviderEntity to use ServiceOwnerId as FK to ServiceOwnerEntity.Id
        modelBuilder.Entity<ServiceOwnerEntity>()
            .HasMany(so => so.StorageProviders)
            .WithOne()
            .HasForeignKey(sp => sp.ServiceOwnerId)
            .HasPrincipalKey(so => so.Id);

        // Ensure simple FK indexes exist for query performance (these are separate from the unique expression indexes)
        modelBuilder.Entity<CorrespondenceDeleteEventEntity>()
            .HasIndex(e => e.CorrespondenceId);
            
        modelBuilder.Entity<CorrespondenceNotificationEntity>()
            .HasIndex(e => e.CorrespondenceId);
            
        modelBuilder.Entity<CorrespondenceForwardingEventEntity>()
            .HasIndex(e => e.CorrespondenceId);

        // Configure unique indexes with second-precision for datetime fields using PostgreSQL expression indexes
        // This allows storing full precision values while enforcing uniqueness at the second level
        
        // CorrespondenceStatusEntity - unique on (CorrespondenceId, Status, date_trunc('second', StatusChanged), PartyUuid)
        modelBuilder.Entity<CorrespondenceStatusEntity>()
            .HasIndex(nameof(CorrespondenceStatusEntity.CorrespondenceId), 
                     nameof(CorrespondenceStatusEntity.Status), 
                     nameof(CorrespondenceStatusEntity.StatusChanged), 
                     nameof(CorrespondenceStatusEntity.PartyUuid))
            .IsUnique()
            .HasDatabaseName("IX_CorrespondenceStatuses_Unique")
            .HasAnnotation("Npgsql:IndexExpression", 
                $"(\"{nameof(CorrespondenceStatusEntity.CorrespondenceId)}\", " +
                $"\"{nameof(CorrespondenceStatusEntity.Status)}\", " +
                $"date_trunc('second', \"{nameof(CorrespondenceStatusEntity.StatusChanged)}\"), " +
                $"\"{nameof(CorrespondenceStatusEntity.PartyUuid)}\")");

        // CorrespondenceDeleteEventEntity - unique on (CorrespondenceId, EventType, date_trunc('second', EventOccurred), PartyUuid)
        modelBuilder.Entity<CorrespondenceDeleteEventEntity>()
            .HasIndex(nameof(CorrespondenceDeleteEventEntity.CorrespondenceId),
                     nameof(CorrespondenceDeleteEventEntity.EventType),
                     nameof(CorrespondenceDeleteEventEntity.EventOccurred),
                     nameof(CorrespondenceDeleteEventEntity.PartyUuid))
            .IsUnique()
            .HasDatabaseName("IX_CorrespondenceDeleteEvents_Unique")
            .HasAnnotation("Npgsql:IndexExpression",
                $"(\"{nameof(CorrespondenceDeleteEventEntity.CorrespondenceId)}\", " +
                $"\"{nameof(CorrespondenceDeleteEventEntity.EventType)}\", " +
                $"date_trunc('second', \"{nameof(CorrespondenceDeleteEventEntity.EventOccurred)}\"), " +
                $"\"{nameof(CorrespondenceDeleteEventEntity.PartyUuid)}\")");

        // CorrespondenceNotificationEntity - unique on (CorrespondenceId, NotificationAddress, NotificationChannel, date_trunc('second', NotificationSent))
        modelBuilder.Entity<CorrespondenceNotificationEntity>()
            .HasIndex(nameof(CorrespondenceNotificationEntity.CorrespondenceId),
                     nameof(CorrespondenceNotificationEntity.NotificationAddress),
                     nameof(CorrespondenceNotificationEntity.NotificationChannel),
                     nameof(CorrespondenceNotificationEntity.NotificationSent))
            .IsUnique()
            .HasDatabaseName("IX_CorrespondenceNotifications_Unique")
            .HasAnnotation("Npgsql:IndexExpression",
                $"(\"{nameof(CorrespondenceNotificationEntity.CorrespondenceId)}\", " +
                $"\"{nameof(CorrespondenceNotificationEntity.NotificationAddress)}\", " +
                $"\"{nameof(CorrespondenceNotificationEntity.NotificationChannel)}\", " +
                $"date_trunc('second', \"{nameof(CorrespondenceNotificationEntity.NotificationSent)}\"))");

        // CorrespondenceForwardingEventEntity - unique on (CorrespondenceId, date_trunc('second', ForwardedOnDate), ForwardedByPartyUuid)
        modelBuilder.Entity<CorrespondenceForwardingEventEntity>()
            .HasIndex(nameof(CorrespondenceForwardingEventEntity.CorrespondenceId),
                     nameof(CorrespondenceForwardingEventEntity.ForwardedOnDate),
                     nameof(CorrespondenceForwardingEventEntity.ForwardedByPartyUuid))
            .IsUnique()
            .HasDatabaseName("IX_CorrespondenceForwardingEvents_Unique")
            .HasAnnotation("Npgsql:IndexExpression",
                $"(\"{nameof(CorrespondenceForwardingEventEntity.CorrespondenceId)}\", " +
                $"date_trunc('second', \"{nameof(CorrespondenceForwardingEventEntity.ForwardedOnDate)}\"), " +
                $"\"{nameof(CorrespondenceForwardingEventEntity.ForwardedByPartyUuid)}\")");
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }
}
