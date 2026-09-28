namespace Altinn.Correspondence.Core.Models;

/// <summary>
/// Aggregated attachment stats for a correspondence, returned from repository queries
/// that need attachment counts/sizes without loading full attachment entities.
/// </summary>
public class CorrespondenceAttachmentStatsDto
{
    public Guid CorrespondenceId { get; set; }
    public DateTimeOffset Created { get; set; }
    public int AttachmentCount { get; set; }
    public long TotalAttachmentSize { get; set; }
}
