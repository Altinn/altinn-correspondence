using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.UpdateOldCorrespondencesWithDownloadAll;

public class UpdateOldCorrespondencesWithDownloadAllRequest
{
    [Range(1, int.MaxValue - 1)]
    public int windowSize { get; set; } = 10000;

    public DateTimeOffset? CursorCreated { get; set; }
    public Guid? CursorId { get; set; }

    public int TotalProcessed { get; set; }
    public int TotalPatched { get; set; }
    public int TotalNotMatchingCriteria { get; set; }
    public int TotalErrors { get; set; }
}