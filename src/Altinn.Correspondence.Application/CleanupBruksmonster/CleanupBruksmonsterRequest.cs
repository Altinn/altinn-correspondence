using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Application.CleanupBruksmonster;

public class CleanupBruksmonsterRequest
{
    public Guid? TestRunId { get; init; }

    [Range(0, 36500)]
    public int? MinAgeDays { get; init; }
} 