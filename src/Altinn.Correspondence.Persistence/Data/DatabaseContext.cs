using Altinn.Correspondence.Core.Models;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<CorrespondenceNotificationEntity> CorrespondenceNotifications { get; set; }
    public DbSet<CorrespondenceNotificationStatusEntity> CorrespondenceNotificationStatuses { get; set; }
    public DbSet<CorrespondenceReplyOptionEntity> CorrespondenceReplyOptions { get; set; }
    public DbSet<ExternalReferenceEntity> ExternalReferences { get; set; }
}