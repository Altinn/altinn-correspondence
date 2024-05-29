using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GetAttachmentOverviewCommand;

public class GetAttachmentOverviewCommandResponse
{
    public required Guid AttachmentId { get; set; }

    public AttachmentDataLocationType DataLocationType { get; set; }

    public string? DataLocationUrl { get; set; }

    public AttachmentStatus? Status { get; set; }

    public string? StatusText { get; set; } = string.Empty;

    public DateTimeOffset? StatusChanged { get; set; }

    public string? Name { get; set; } = string.Empty;

    public string SendersReference { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public IntendedPresentationType IntendedPresentation { get; set; }
}
