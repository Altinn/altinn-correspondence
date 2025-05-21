using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;


namespace Altinn.Correspondence.Core.Models.Entities;

public class IdempotencyKeyEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid? CorrespondenceId { get; set; }

    [ForeignKey(nameof(CorrespondenceId))]
    public CorrespondenceEntity? Correspondence { get; set; }

    public Guid? AttachmentId { get; set; }

    [ForeignKey(nameof(AttachmentId))]
    public AttachmentEntity? Attachment { get; set; }

    public StatusAction? StatusAction { get; set; }

    [Required]
    public IdempotencyType IdempotencyType { get; set; }
} 