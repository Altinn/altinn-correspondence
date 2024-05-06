using Altinn.Correspondence.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
    public DbSet<AttachmentEntity> Attachments { get; set; }
    public DbSet<AttachmentStatusEntity> AttachmentStatuses { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}