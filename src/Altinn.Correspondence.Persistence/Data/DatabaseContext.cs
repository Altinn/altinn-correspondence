using Altinn.Correspondence.Core.Models;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
    public DbSet<AttachmentEntity> Attachments { get; set; }
    public DbSet<AttachmentStatusEntity> AttachmentStatuses { get; set; }
    public DbSet<CorrespondenceContentEntity> CorrespondenceContents { get; set; }
    public DbSet<CorrespondenceEntity> Correspondences { get; set; }
    public DbSet<CorrespondenceStatusEntity> CorrespondenceStatuses { get; set; }
    public DbSet<CorrespondenceNotificationEntity> CorrespondenceNotifications { get; set; }
    public DbSet<CorrespondenceReplyOptionEntity> CorrespondenceReplyOptions { get; set; }
    public DbSet<ExternalReferenceEntity> ExternalReferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttachmentEntity>()
            .Property(b => b.Created)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<CorrespondenceEntity>()
            .Property(b => b.Created)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<CorrespondenceNotificationEntity>()
            .Property(b => b.Created)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<AttachmentStatusEntity>()
            .Property(b => b.StatusChanged)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<CorrespondenceStatusEntity>()
            .Property(b => b.StatusChanged)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<CorrespondenceNotificationStatusEntity>()
            .Property(b => b.StatusChanged)
            .HasDefaultValueSql("NOW()");
    }
}