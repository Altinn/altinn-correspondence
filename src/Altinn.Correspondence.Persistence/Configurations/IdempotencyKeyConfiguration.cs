using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Altinn.Correspondence.Core.Models.Entities;

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
            .IsRequired(false)
            .HasConversion<string>();

        builder.Property(x => x.IdempotencyType)
            .IsRequired();

        // Create a unique index on the combination of CorrespondenceId, AttachmentId, and Action
        // Only for DialogportenActivity type
        builder.HasIndex(x => new { x.CorrespondenceId, x.AttachmentId, x.StatusAction })
            .IsUnique()
            .HasFilter("[IdempotencyType] = 0"); // 0 = DialogportenActivity
    }
} 