using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsResponse
{
    public required string ResourceId { get; set; }

    public required Guid AttachmentId { get; set; }

    public AttachmentDataLocationType DataLocationType { get; set; }

    public AttachmentStatus Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTimeOffset StatusChanged { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string SendersReference { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public bool IsEncrypted { get; set; }

    public string Checksum { get; set; } = string.Empty;

    public List<AttachmentStatusEntity> Statuses { get; set; } = new List<AttachmentStatusEntity>();
    public List<Guid> CorrespondenceIds { get; set; } = new List<Guid>();
}
