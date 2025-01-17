using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetAttachmentOverview;

public class GetAttachmentOverviewResponse
{
    public required Guid AttachmentId { get; set; }
    public required string ResourceId { get; set; }

    public AttachmentDataLocationType DataLocationType { get; set; }

    public AttachmentStatus Status { get; set; }

    public required string StatusText { get; set; }

    public DateTimeOffset StatusChanged { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? Name { get; set; } = string.Empty;
    
    public string? Checksum { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string SendersReference { get; set; } = string.Empty;

    public List<Guid> CorrespondenceIds { get; set; } = new List<Guid>();
}
