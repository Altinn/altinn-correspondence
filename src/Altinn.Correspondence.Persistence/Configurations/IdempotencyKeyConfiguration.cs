using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Persistence.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.ToTable("IdempotencyKeys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CorrespondenceId)
            .IsRequired();

        builder.Property(x => x.AttachmentId)
            .IsRequired(false);

        builder.Property(x => x.StatusAction)
            .IsRequired()
            .HasConversion<string>();

        // Create a unique index on the combination of CorrespondenceId, AttachmentId, and Action
        builder.HasIndex(x => new { x.CorrespondenceId, x.AttachmentId, x.StatusAction })
            .IsUnique();
    }
} 